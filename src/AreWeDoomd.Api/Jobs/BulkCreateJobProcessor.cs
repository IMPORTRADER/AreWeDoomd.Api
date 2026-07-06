using System.Security.Cryptography;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Api.Jobs;

public sealed class BulkCreateJobProcessor(
    IPersonaGenerator generator,
    IServiceScopeFactory scopeFactory,
    BulkCreateJobStore store,
    IOptions<PersonaGenerationOptions> options,
    IAgentOpsLogger opsLog,
    ILogger<BulkCreateJobProcessor> logger)
{
    private readonly PersonaGenerationOptions _options = options.Value;

    public async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        var snap = store.TryGetSnapshot(jobId);
        if (snap == null)
        {
            logger.LogWarning("BulkCreateJob {JobId} not found in store — skipping.", jobId);
            return;
        }

        int requested = snap.Requested;
        store.MarkStarted(jobId, DateTimeOffset.UtcNow);
        opsLog.TryLog(new AgentOpsLogRecord(
            DateTimeOffset.UtcNow, AgentOpsLogLevels.Info, AgentOpsLogSources.Admin,
            $"Bulk create started: {requested} AI user(s) requested (job {jobId})."));

        int emptyBatchStreak = 0;

        while (store.GetCreatedPlusFailed(jobId) < requested)
        {
            int remaining = requested - store.GetCreatedPlusFailed(jobId);
            int toGenerate = Math.Min(_options.BatchSize, remaining);

            store.SetStatus(jobId, "generating");
            var genResult = await generator.GenerateBatchAsync(toGenerate, ct);

            if (!genResult.IsSuccess)
            {
                logger.LogWarning(
                    "Persona generator failed for job {JobId}: {Error}. Recording remaining as failed.",
                    jobId, genResult.Error!.Message);

                int rem = requested - store.GetCreatedPlusFailed(jobId);
                for (int i = 0; i < rem; i++)
                {
                    store.RecordFailed(jobId, null, genResult.Error.Message);
                }

                break;
            }

            store.SetStatus(jobId, "creating");
            var personas = genResult.Value!;
            store.IncrementGenerated(jobId, personas.Count);

            var batchSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int usedInBatch = 0;

            foreach (var persona in personas)
            {
                if (store.GetCreatedPlusFailed(jobId) >= requested)
                {
                    break;
                }

                if (!batchSeen.Add(persona.Username))
                {
                    continue; // batch-level dedupe
                }

                usedInBatch++;
                await TryCreateUserAsync(jobId, persona, ct);
            }

            if (usedInBatch == 0)
            {
                emptyBatchStreak++;
                if (emptyBatchStreak >= 2)
                {
                    logger.LogWarning(
                        "Job {JobId}: two consecutive batches yielded 0 usable personas. Failing remaining.",
                        jobId);

                    int rem = requested - store.GetCreatedPlusFailed(jobId);
                    for (int i = 0; i < rem; i++)
                    {
                        store.RecordFailed(jobId, null,
                            "No usable personas produced in two consecutive batches.");
                    }

                    break;
                }
            }
            else
            {
                emptyBatchStreak = 0;
            }
        }

        store.Complete(jobId, DateTimeOffset.UtcNow);
        opsLog.TryLog(new AgentOpsLogRecord(
            DateTimeOffset.UtcNow, AgentOpsLogLevels.Info, AgentOpsLogSources.Admin,
            $"Bulk create finished: {store.GetCreatedPlusFailed(jobId)}/{requested} processed (job {jobId})."));
    }

    private async Task TryCreateUserAsync(Guid jobId, GeneratedPersona persona, CancellationToken ct)
    {
        bool created = await AttemptUserCreationAsync(jobId, persona.Username, persona, ct, recordFailure: false);
        if (created)
        {
            return;
        }

        // One retry with a random 4-hex suffix
        var suffixBytes = new byte[2];
        RandomNumberGenerator.Fill(suffixBytes);
        var suffix = Convert.ToHexString(suffixBytes).ToLowerInvariant();
        var retryUsername = persona.Username + suffix;
        if (retryUsername.Length > 24)
        {
            retryUsername = retryUsername[..24];
        }

        await AttemptUserCreationAsync(jobId, retryUsername, persona, ct, recordFailure: true);
    }

    private async Task<bool> AttemptUserCreationAsync(
        Guid jobId, string username, GeneratedPersona persona, CancellationToken ct, bool recordFailure)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var userRepo = sp.GetRequiredService<IUserRepository>();
        var factory = sp.GetRequiredService<IAiAccountFactory>();
        var uow = sp.GetRequiredService<IUnitOfWork>();
        var dateTime = sp.GetRequiredService<IDateTimeProvider>();
        var recordRepo = sp.GetRequiredService<IBulkCreationRecordRepository>();
        var now = dateTime.UtcNow;

        if (await userRepo.IsUsernameTakenAsync(username, null, ct))
        {
            if (recordFailure)
            {
                store.RecordFailed(jobId, username, "Username collision: taken after suffix retry.");
            }

            return false;
        }

        var email = $"{username}@ai.arewedoomd.local";
        var passwordBytes = new byte[32];
        RandomNumberGenerator.Fill(passwordBytes);
        var password = Convert.ToHexString(passwordBytes).ToLowerInvariant();

        var factoryResult = await factory.CreateAiAccountAsync(username, email, password, now, ct);
        if (!factoryResult.IsSuccess)
        {
            if (recordFailure)
            {
                store.RecordFailed(jobId, username, factoryResult.Error!.Message);
            }

            return false;
        }

        var user = factoryResult.Value!;
        user.SetAiPersonality(persona.Traits, persona.TypingStyle, persona.Summary, now);
        await recordRepo.AddAsync(BulkCreationRecord.Create(jobId, user.Id, user.Username, now), ct);

        try
        {
            await uow.SaveChangesAsync(ct);
            store.RecordCreated(jobId, user.Username);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogWarning(
                "Unique-violation saving {Username} in job {JobId}.", username, jobId);

            if (recordFailure)
            {
                store.RecordFailed(jobId, username, "Username collision at database level after retry.");
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating user {Username} in job {JobId}.", username, jobId);

            if (recordFailure)
            {
                store.RecordFailed(jobId, username, ex.Message);
            }

            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("2601", StringComparison.Ordinal)
            || msg.Contains("2627", StringComparison.Ordinal)
            || msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("UNIQUE", StringComparison.Ordinal);
    }
}

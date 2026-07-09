using System.Security.Cryptography;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.Api.Jobs;

public sealed class BulkCreateJobProcessor(
    IPersonaFactory personaFactory,
    IServiceScopeFactory scopeFactory,
    BulkCreateJobStore store,
    IAgentOpsLogger opsLog,
    ILogger<BulkCreateJobProcessor> logger)
{
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

        store.SetStatus(jobId, "creating");

        while (store.GetCreatedPlusFailed(jobId) < requested)
        {
            var persona = personaFactory.CreateRandom();
            store.IncrementGenerated(jobId, 1);
            await TryCreateUserAsync(jobId, persona, ct);
        }

        store.Complete(jobId, DateTimeOffset.UtcNow);
        var finalSnap = store.TryGetSnapshot(jobId);
        int createdCount = finalSnap?.Created ?? 0;
        int failedCount = finalSnap?.Failed.Count ?? 0;
        opsLog.TryLog(new AgentOpsLogRecord(
            DateTimeOffset.UtcNow,
            failedCount > 0 ? AgentOpsLogLevels.Warning : AgentOpsLogLevels.Info,
            AgentOpsLogSources.Admin,
            $"Bulk create finished: {createdCount} created, {failedCount} failed of {requested} requested (job {jobId})."));
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

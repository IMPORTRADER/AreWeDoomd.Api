using AreWeDoomd.Api.Jobs;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.UnitTests.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Api.Jobs;

public sealed class BulkCreateJobProcessorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");

    private static readonly PersonaGenerationOptions DefaultOptions = new()
    {
        BatchSize = 10,
        MaxCount = 50,
        Provider = "openrouter"
    };

    private readonly Mock<IPersonaGenerator> _generator = new();
    private readonly Mock<IAiAccountFactory> _factory = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IDateTimeProvider> _dateTime = new();
    private readonly Mock<IBulkCreationRecordRepository> _recordRepo = new();

    public BulkCreateJobProcessorTests()
    {
        _dateTime.Setup(d => d.UtcNow).Returns(Now);
        _userRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _recordRepo
            .Setup(r => r.AddAsync(It.IsAny<BulkCreationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private IServiceScopeFactory BuildScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUserRepository>(_ => _userRepo.Object);
        services.AddScoped<IAiAccountFactory>(_ => _factory.Object);
        services.AddScoped<IUnitOfWork>(_ => _uow.Object);
        services.AddScoped<IDateTimeProvider>(_ => _dateTime.Object);
        services.AddScoped<IBulkCreationRecordRepository>(_ => _recordRepo.Object);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private BulkCreateJobProcessor CreateProcessor(BulkCreateJobStore? store = null, PersonaGenerationOptions? opts = null)
    {
        store ??= new BulkCreateJobStore();
        return new BulkCreateJobProcessor(
            _generator.Object,
            BuildScopeFactory(),
            store,
            Options.Create(opts ?? DefaultOptions),
            new StubAgentOpsLogger(),
            NullLogger<BulkCreateJobProcessor>.Instance);
    }

    private static GeneratedPersona MakePersona(string username) =>
        new(username, ["curious", "witty"], "casual style", "A curious bot.");

    private static User MakeAiUser(string username) =>
        User.Create(username, $"{username}@ai.arewedoomd.local", "valid-hash-string-1234", UserType.Ai, Now);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_HappyPath_3Personas_AllCreated()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 3);

        var personas = new List<GeneratedPersona>
        {
            MakePersona("alpha"), MakePersona("beta"), MakePersona("gamma")
        };

        _generator
            .Setup(g => g.GenerateBatchAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GeneratedPersona>>.Success(personas));

        _factory.Setup(f => f.CreateAiAccountAsync("alpha", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(MakeAiUser("alpha")));
        _factory.Setup(f => f.CreateAiAccountAsync("beta", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(MakeAiUser("beta")));
        _factory.Setup(f => f.CreateAiAccountAsync("gamma", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(MakeAiUser("gamma")));

        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        var snap = store.TryGetSnapshot(jobId)!;
        snap.Status.ShouldBe("completed");
        snap.Created.ShouldBe(3);
        snap.Failed.ShouldBeEmpty();
        snap.CreatedUsers.ShouldContain("alpha");
        snap.CreatedUsers.ShouldContain("beta");
        snap.CreatedUsers.ShouldContain("gamma");
    }

    [Fact]
    public async Task ProcessAsync_HappyPath_RecordAddAsyncCalledPerUser()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 3);

        var personas = new List<GeneratedPersona>
        {
            MakePersona("alpha"), MakePersona("beta"), MakePersona("gamma")
        };

        _generator
            .Setup(g => g.GenerateBatchAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GeneratedPersona>>.Success(personas));

        foreach (var name in new[] { "alpha", "beta", "gamma" })
        {
            _factory.Setup(f => f.CreateAiAccountAsync(name, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<User>.Success(MakeAiUser(name)));
        }

        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        _recordRepo.Verify(
            r => r.AddAsync(It.IsAny<BulkCreationRecord>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessAsync_HappyPath_RecordContainsCorrectJobIdAndUsername()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 1);

        _generator
            .Setup(g => g.GenerateBatchAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GeneratedPersona>>.Success(
                new List<GeneratedPersona> { MakePersona("trackme") }));

        var user = MakeAiUser("trackme");
        _factory.Setup(f => f.CreateAiAccountAsync("trackme", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(user));

        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        BulkCreationRecord? capturedRecord = null;
        _recordRepo
            .Setup(r => r.AddAsync(It.IsAny<BulkCreationRecord>(), It.IsAny<CancellationToken>()))
            .Callback<BulkCreationRecord, CancellationToken>((rec, _) => capturedRecord = rec)
            .Returns(Task.CompletedTask);

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        capturedRecord.ShouldNotBeNull();
        capturedRecord!.JobId.ShouldBe(jobId);
        capturedRecord.UserId.ShouldBe(user.Id);
        capturedRecord.Username.ShouldBe("trackme");
    }

    // ── collision / retry ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_UniqueViolation_RetriesWithSuffix_ThenSucceeds()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 1);

        var personas = new List<GeneratedPersona> { MakePersona("collision") };

        _generator
            .Setup(g => g.GenerateBatchAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GeneratedPersona>>.Success(personas));

        // First attempt (original username) → SaveChanges throws unique violation
        _factory.Setup(f => f.CreateAiAccountAsync("collision", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(MakeAiUser("collision")));

        // Retry attempt (suffix username) → succeeds
        _factory.Setup(f => f.CreateAiAccountAsync(
                It.Is<string>(u => u.StartsWith("collision") && u.Length > "collision".Length),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string u, string _, string _, DateTimeOffset _, CancellationToken _) =>
                Result<User>.Success(MakeAiUser(u)));

        var callCount = 0;
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new DbUpdateException("unique constraint violation 2601",
                        new Exception("2601"));
                }
                return 1;
            });

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        var snap = store.TryGetSnapshot(jobId)!;
        snap.Created.ShouldBe(1);
        snap.Status.ShouldBe("completed");
        snap.Failed.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_BothAttemptsCollide_RecordsFailure()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 1);

        var personas = new List<GeneratedPersona> { MakePersona("collision2") };

        _generator
            .Setup(g => g.GenerateBatchAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GeneratedPersona>>.Success(personas));

        // Both attempts return a user but SaveChanges always throws
        _factory.Setup(f => f.CreateAiAccountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string u, string _, string _, DateTimeOffset _, CancellationToken _) =>
                Result<User>.Success(MakeAiUser(u)));

        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("unique constraint 2627", new Exception("2627")));

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        var snap = store.TryGetSnapshot(jobId)!;
        snap.Created.ShouldBe(0);
        snap.Failed.Count.ShouldBe(1);
        snap.Status.ShouldBe("completed");
    }

    // ── generator failure mid-job ──────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_GeneratorFailure_RecordsRemainingAsFailed_Completes()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 5);

        _generator
            .Setup(g => g.GenerateBatchAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GeneratedPersona>>.Failure(
                "persona.generation_failed", "Provider error."));

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        var snap = store.TryGetSnapshot(jobId)!;
        snap.Status.ShouldBe("completed");
        snap.Created.ShouldBe(0);
        snap.Failed.Count.ShouldBe(5);
        snap.Failed.ShouldAllBe(f => f.Reason == "Provider error.");
    }

    // ── zero-usable-persona termination ───────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_TwoConsecutiveEmptyBatches_FailsRemaining_Terminates()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 3);

        // Generator always returns an empty batch (0 usable personas per call)
        _generator
            .Setup(g => g.GenerateBatchAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GeneratedPersona>>.Success(
                new List<GeneratedPersona>()));

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        var snap = store.TryGetSnapshot(jobId)!;
        snap.Status.ShouldBe("completed");
        snap.Created.ShouldBe(0);
        // After 2 empty batches, remaining (3) are recorded as failed
        snap.Failed.Count.ShouldBe(3);
        snap.Failed.ShouldAllBe(f => f.Reason.Contains("consecutive"));
    }

    // ── snapshot point-in-time safety ──────────────────────────────────────────

    [Fact]
    public void ToSnapshot_IsPointInTimeCopy_ImmuneToConcurrentMutations()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 10);

        // Take a snapshot with empty state
        var snap1 = store.TryGetSnapshot(jobId)!;
        snap1.Created.ShouldBe(0);
        snap1.CreatedUsers.Count.ShouldBe(0);
        snap1.Failed.Count.ShouldBe(0);

        // Mutate the store (as background processor would do)
        store.RecordCreated(jobId, "user1");
        store.RecordCreated(jobId, "user2");
        store.RecordFailed(jobId, null, "test failure 1");
        store.RecordFailed(jobId, null, "test failure 2");

        // The previously-taken snapshot should be UNCHANGED (point-in-time copy)
        snap1.Created.ShouldBe(0);
        snap1.CreatedUsers.Count.ShouldBe(0);
        snap1.Failed.Count.ShouldBe(0);

        // A new snapshot reflects the mutations
        var snap2 = store.TryGetSnapshot(jobId)!;
        snap2.Created.ShouldBe(2);
        snap2.CreatedUsers.Count.ShouldBe(2);
        snap2.Failed.Count.ShouldBe(2);
    }
}

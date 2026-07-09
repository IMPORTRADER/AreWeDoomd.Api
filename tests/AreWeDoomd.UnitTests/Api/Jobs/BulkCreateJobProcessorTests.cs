using AreWeDoomd.Api.Jobs;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.UnitTests.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Api.Jobs;

public sealed class BulkCreateJobProcessorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");

    private readonly Mock<IPersonaFactory> _personaFactory = new();
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
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
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

    private BulkCreateJobProcessor CreateProcessor(BulkCreateJobStore? store = null)
    {
        store ??= new BulkCreateJobStore();
        return new BulkCreateJobProcessor(
            _personaFactory.Object,
            BuildScopeFactory(),
            store,
            new StubAgentOpsLogger(),
            NullLogger<BulkCreateJobProcessor>.Instance);
    }

    private static GeneratedPersona MakePersona(string username) =>
        new(username, ["curious", "witty", "dry"], "casual style", "A curious bot.");

    private static User MakeAiUser(string username) =>
        User.Create(username, $"{username}@ai.arewedoomd.local", "valid-hash-string-1234", UserType.Ai, Now);

    private void SetupSuccessfulAccountCreation()
    {
        _factory.Setup(f => f.CreateAiAccountAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string username, string _, string _, DateTimeOffset _, CancellationToken _) =>
                Result<User>.Success(MakeAiUser(username)));
    }

    [Fact]
    public async Task ProcessAsync_HappyPath_3Requested_AllCreated()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 3);

        var queue = new Queue<GeneratedPersona>([MakePersona("alpha"), MakePersona("beta"), MakePersona("gamma")]);
        _personaFactory.Setup(f => f.CreateRandom()).Returns(() => queue.Dequeue());
        SetupSuccessfulAccountCreation();

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
        store.Create(jobId, 2);

        var queue = new Queue<GeneratedPersona>([MakePersona("alpha"), MakePersona("beta")]);
        _personaFactory.Setup(f => f.CreateRandom()).Returns(() => queue.Dequeue());
        SetupSuccessfulAccountCreation();

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        _recordRepo.Verify(
            r => r.AddAsync(It.IsAny<BulkCreationRecord>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_UsernameTaken_RetriesWithSuffixAndSucceeds()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 1);

        _personaFactory.Setup(f => f.CreateRandom()).Returns(MakePersona("taken"));
        _userRepo.Setup(r => r.IsUsernameTakenAsync("taken", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepo.Setup(r => r.IsUsernameTakenAsync(
                It.Is<string>(u => u.StartsWith("taken") && u.Length > 5), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        SetupSuccessfulAccountCreation();

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        var snap = store.TryGetSnapshot(jobId)!;
        snap.Created.ShouldBe(1);
        snap.Failed.ShouldBeEmpty();
        snap.CreatedUsers[0].ShouldStartWith("taken");
        snap.CreatedUsers[0].Length.ShouldBeGreaterThan("taken".Length);
    }

    [Fact]
    public async Task ProcessAsync_AccountFactoryFailsTwice_RecordsFailed()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 1);

        _personaFactory.Setup(f => f.CreateRandom()).Returns(MakePersona("badstate"));
        _factory.Setup(f => f.CreateAiAccountAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Failure("user.invalid", "Account creation failed."));

        var processor = CreateProcessor(store);
        await processor.ProcessAsync(jobId, default);

        var snap = store.TryGetSnapshot(jobId)!;
        snap.Status.ShouldBe("completed");
        snap.Created.ShouldBe(0);
        snap.Failed.Count.ShouldBe(1);
        snap.Failed[0].Reason.ShouldBe("Account creation failed.");
    }

    [Fact]
    public async Task ProcessAsync_UniqueViolation_RetriesWithSuffix_ThenSucceeds()
    {
        var store = new BulkCreateJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, 1);

        _personaFactory.Setup(f => f.CreateRandom()).Returns(MakePersona("collision"));

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

        _personaFactory.Setup(f => f.CreateRandom()).Returns(MakePersona("collision2"));

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

    [Fact]
    public async Task ProcessAsync_UnknownJob_ReturnsWithoutThrowing()
    {
        var processor = CreateProcessor();
        await processor.ProcessAsync(Guid.NewGuid(), default);
        // no exception = pass
    }

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

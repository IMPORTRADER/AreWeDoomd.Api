using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Commands.StartScheduleRun;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using AreWeDoomd.UnitTests.Application;
using Moq;
using Shouldly;
using Xunit;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.UnitTests.Features.PostScheduling.Commands.StartScheduleRun;

public sealed class StartScheduleRunCommandHandlerTests
{
    // 12:00 UTC = 15:00 TRT → RunDate 2026-07-05, pencere sonu 21:00 UTC
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Ai1 = Guid.NewGuid();

    private readonly Mock<IScheduleRunRepository> _runRepo = new();
    private readonly Mock<IScheduledPostRepository> _postRepo = new();
    private readonly Mock<ISchedulingSettingsRepository> _settingsRepo = new();
    private readonly Mock<ILlmSettingsRepository> _llmSettings = new();
    private readonly Mock<IScheduleTargetReadRepository> _targets = new();
    private readonly Mock<IScheduleRunHubSender> _hub = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly StartScheduleRunCommandHandler _handler;

    public StartScheduleRunCommandHandlerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _settingsRepo.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SchedulingSettings.CreateDefault(Now));
        _llmSettings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainLlmSettings?)null);
        _targets.Setup(t => t.GetTargetsAsync(It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScheduleTarget(Ai1, "ai-1", "özet", 2, Now.AddDays(-1))]);
        _runRepo.Setup(r => r.GetActiveItemsForDateAsync(It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _uow.Setup(u => u.TrySaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _handler = new StartScheduleRunCommandHandler(
            _runRepo.Object, _postRepo.Object, _settingsRepo.Object, _llmSettings.Object, _targets.Object,
            _hub.Object, _clock.Object, _uow.Object, new StubAgentOpsLogger());
    }

    [Fact]
    public async Task Handle_WhenNoConflicts_ShouldPersistRunAndPushHubMessage()
    {
        var result = await _handler.Handle(new StartScheduleRunCommand(Admin, null, false), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ItemCount.ShouldBe(1);
        _runRepo.Verify(r => r.AddAsync(It.Is<ScheduleRun>(x => x.Items.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.TrySaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _hub.Verify(h => h.SendAsync(
            It.Is<ScheduleRunRequest>(m => m.Items.Count == 1 && m.Items[0].AiUserId == Ai1 && m.Threshold == 60),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExistingItemsAndNoOverwrite_ShouldReturnConflict()
    {
        var existing = ScheduleRunItem.Create(Guid.NewGuid(), Ai1, new DateOnly(2026, 7, 5), Now);
        _runRepo.Setup(r => r.GetActiveItemsForDateAsync(It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        var result = await _handler.Handle(new StartScheduleRunCommand(Admin, null, false), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        _hub.Verify(h => h.SendAsync(It.IsAny<ScheduleRunRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOverwrite_ShouldSupersedeExistingAndCancelPendingPosts()
    {
        var existing = ScheduleRunItem.Create(Guid.NewGuid(), Ai1, new DateOnly(2026, 7, 5), Now);
        var pendingPost = ScheduledPost.Create(existing.Id, Ai1, "eski", Now.AddHours(3), false, Now);
        _runRepo.Setup(r => r.GetActiveItemsForDateAsync(It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);
        _postRepo.Setup(p => p.GetPendingByRunItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingPost]);

        var result = await _handler.Handle(new StartScheduleRunCommand(Admin, null, true), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        existing.Status.ShouldBe(ScheduleRunItemStatus.Superseded);
        pendingPost.Status.ShouldBe(ScheduledPostStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_WhenWindowNearlyClosed_ShouldReturnFailureWithoutLlm()
    {
        // 20:45 UTC = 23:45 TRT → kalan pencere 15 dk < 30 dk → LLM çağrısına para verilmez
        _clock.Setup(c => c.UtcNow).Returns(new DateTimeOffset(2026, 7, 5, 20, 45, 0, TimeSpan.Zero));

        var result = await _handler.Handle(new StartScheduleRunCommand(Admin, null, false), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Failure);
        _hub.Verify(h => h.SendAsync(It.IsAny<ScheduleRunRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoTargets_ShouldReturnNotFound()
    {
        _targets.Setup(t => t.GetTargetsAsync(It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new StartScheduleRunCommand(Admin, [Guid.NewGuid()], false), CancellationToken.None);

        result.ErrorType.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenTrySaveReturnsFalse_ShouldReturnConflictAndNotPushHub()
    {
        // Simulates a concurrent request winning the unique-index race.
        _uow.Setup(u => u.TrySaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _handler.Handle(new StartScheduleRunCommand(Admin, null, false), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        _hub.Verify(h => h.SendAsync(It.IsAny<ScheduleRunRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSnapshotLlmSettingsIntoHubMessage()
    {
        var llm = DomainLlmSettings.CreateDefault(Now);
        llm.Update("openai/gpt-oss-120b:free", "meta-llama/llama-3.3-70b-instruct:free",
            thinkingEnabled: true, scoringTokensPerAccount: 256, compositionTokensPerPost: 900,
            replyMaxTokens: 1024, now: Now);
        _llmSettings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(llm);

        var result = await _handler.Handle(new StartScheduleRunCommand(Admin, null, false), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _hub.Verify(h => h.SendAsync(
            It.Is<ScheduleRunRequest>(m =>
                m.Model == "openai/gpt-oss-120b:free" &&
                m.ScoringModel == "meta-llama/llama-3.3-70b-instruct:free" &&
                m.ThinkingEnabled &&
                m.ScoringTokensPerAccount == 256 &&
                m.CompositionTokensPerPost == 900),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAllConflictingItemsFailed_ShouldReturnFailedPlanConflictMessage()
    {
        var existing = ScheduleRunItem.Create(Guid.NewGuid(), Ai1, new DateOnly(2026, 7, 5), Now);
        existing.MarkFailed("LLM scoring failed", Now);
        _runRepo.Setup(r => r.GetActiveItemsForDateAsync(It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        var result = await _handler.Handle(new StartScheduleRunCommand(Admin, null, false), CancellationToken.None);

        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Message.ShouldContain("başarısız olmuştu");
        result.Error!.Message.ShouldContain("Üzerine yazarak yeniden deneyebilirsiniz");
    }
}

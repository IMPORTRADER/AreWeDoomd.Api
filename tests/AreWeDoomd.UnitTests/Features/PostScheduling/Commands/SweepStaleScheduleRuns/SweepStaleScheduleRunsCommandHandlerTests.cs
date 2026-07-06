using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.PostScheduling.Commands.SweepStaleScheduleRuns;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using Moq;
using Shouldly;
using Xunit;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.UnitTests.Features.PostScheduling.Commands.SweepStaleScheduleRuns;

public sealed class SweepStaleScheduleRunsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ai1 = Guid.NewGuid();

    private readonly Mock<IScheduleRunRepository> _runRepo = new();
    private readonly Mock<IScheduleTargetReadRepository> _targets = new();
    private readonly Mock<IScheduleRunHubSender> _hub = new();
    private readonly Mock<ILlmSettingsRepository> _llmSettings = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly SweepStaleScheduleRunsCommandHandler _handler;
    private readonly ScheduleRun _run;
    private readonly ScheduleRunItem _item;

    public SweepStaleScheduleRunsCommandHandlerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _llmSettings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainLlmSettings?)null);
        _run = ScheduleRun.Create(new DateOnly(2026, 7, 5), Guid.NewGuid(), 60, 3, 500,
            LlmSchedulingStrategy.TwoStage, Now.AddMinutes(-30));
        _item = _run.AddItem(Ai1, Now.AddMinutes(-30));
        _runRepo.Setup(r => r.GetRunningRunsWithItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_run]);
        _targets.Setup(t => t.GetTargetsAsync(It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScheduleTarget(Ai1, "ai-1", "özet", 1, null)]);
        _handler = new SweepStaleScheduleRunsCommandHandler(
            _runRepo.Object, _targets.Object, _hub.Object, _llmSettings.Object, _clock.Object, _uow.Object);
    }

    [Fact]
    public async Task Handle_WhenItemStaleWithSinglePush_ShouldRepushViaHub()
    {
        // LastPushedAt = Now-30dk; timeout = 5dk taban + 1dk/hesap → 6dk; 30dk > 6dk → stale
        await _handler.Handle(new SweepStaleScheduleRunsCommand(), CancellationToken.None);

        _item.PushCount.ShouldBe(2);
        _item.Status.ShouldBe(ScheduleRunItemStatus.AwaitingLlm);
        _hub.Verify(h => h.SendAsync(
            It.Is<ScheduleRunRequest>(m => m.Items.Count == 1 && m.Items[0].RunItemId == _item.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenItemStaleAfterRepush_ShouldFailItemAndCompleteRun()
    {
        _item.RecordRepush(Now.AddMinutes(-20)); // PushCount = 2, hâlâ yanıt yok

        await _handler.Handle(new SweepStaleScheduleRunsCommand(), CancellationToken.None);

        _item.Status.ShouldBe(ScheduleRunItemStatus.Failed);
        _run.Status.ShouldBe(ScheduleRunStatus.CompletedWithErrors);
        _hub.Verify(h => h.SendAsync(It.IsAny<ScheduleRunRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenItemFresh_ShouldDoNothing()
    {
        _item.RecordRepush(Now.AddMinutes(-1)); // 1 dk önce push'landı — henüz stale değil

        await _handler.Handle(new SweepStaleScheduleRunsCommand(), CancellationToken.None);

        _item.Status.ShouldBe(ScheduleRunItemStatus.AwaitingLlm);
        _hub.Verify(h => h.SendAsync(It.IsAny<ScheduleRunRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenItemStaleWithSinglePush_ShouldRepushWithConfiguredLlmSettings()
    {
        var llm = DomainLlmSettings.CreateDefault(Now);
        llm.Update("custom/model", "", false, 256, 800, 700, 1024, Now);
        _llmSettings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(llm);

        await _handler.Handle(new SweepStaleScheduleRunsCommand(), CancellationToken.None);

        _hub.Verify(h => h.SendAsync(
            It.Is<ScheduleRunRequest>(m => m.Model == "custom/model" && m.ScoringTokensPerAccount == 256),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

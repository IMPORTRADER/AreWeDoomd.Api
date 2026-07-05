using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Commands.SubmitScheduleDecision;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using MediatR;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.PostScheduling.Commands.SubmitScheduleDecision;

public sealed class SubmitScheduleDecisionCommandHandlerTests
{
    // 12:00 UTC = 15:00 TRT; RunDate = 2026-07-05; pencere sonu 2026-07-05T21:00Z
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly RunDate = new(2026, 7, 5);
    private static readonly Guid Ai1 = Guid.NewGuid();

    private readonly Mock<IScheduleRunRepository> _runRepo = new();
    private readonly Mock<IScheduledPostRepository> _postRepo = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISchedulePublisherWaker> _waker = new();
    private readonly SubmitScheduleDecisionCommandHandler _handler;
    private readonly ScheduleRun _run;
    private readonly ScheduleRunItem _item;

    public SubmitScheduleDecisionCommandHandlerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _run = ScheduleRun.Create(RunDate, Guid.NewGuid(), thresholdSnapshot: 60,
            maxPostsSnapshot: 3, postLengthGuideSnapshot: 500, LlmSchedulingStrategy.TwoStage, Now);
        _item = _run.AddItem(Ai1, Now);
        _runRepo.Setup(r => r.GetRunOfItemAsync(_item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_run);
        _handler = new SubmitScheduleDecisionCommandHandler(
            _runRepo.Object, _postRepo.Object, _clock.Object, _uow.Object, _waker.Object);
    }

    private static SubmitScheduleDecisionCommand Cmd(
        Guid itemId, Guid caller, int score, IReadOnlyList<SubmittedScheduledPost> posts, string? error = null)
        => new(itemId, caller, score, "reason", posts.Count, "model-x", error, posts);

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnItem_ShouldReturnForbidden()
    {
        var result = await _handler.Handle(
            Cmd(_item.Id, Guid.NewGuid(), 80, [new("içerik", Now.AddHours(2))]), CancellationToken.None);
        result.ErrorType.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenItemAlreadyTerminal_ShouldReturnConflict()
    {
        _item.RecordDecision(70, "r", 1, 0, "m", true, 1, Now);
        var result = await _handler.Handle(
            Cmd(_item.Id, Ai1, 80, [new("içerik", Now.AddHours(2))]), CancellationToken.None);
        result.ErrorType.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_WhenScoreBelowSnapshotThreshold_ShouldSetBelowThresholdAndAddNoPosts()
    {
        var result = await _handler.Handle(Cmd(_item.Id, Ai1, 45, []), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _item.Status.ShouldBe(ScheduleRunItemStatus.BelowThreshold);
        _item.DesireScore.ShouldBe(45);
        _postRepo.Verify(p => p.AddAsync(It.IsAny<ScheduledPost>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidPosts_ShouldPersistAndWakePublisher()
    {
        var result = await _handler.Handle(
            Cmd(_item.Id, Ai1, 80, [new("bir", Now.AddHours(3)), new("iki", Now.AddHours(6))]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _item.Status.ShouldBe(ScheduleRunItemStatus.Completed);
        _postRepo.Verify(p => p.AddAsync(It.IsAny<ScheduledPost>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _waker.Verify(w => w.Wake(), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTimeInPastButSameTurkeyDay_ShouldClampToNowPlus5AndFlag()
    {
        ScheduledPost? added = null;
        _postRepo.Setup(p => p.AddAsync(It.IsAny<ScheduledPost>(), It.IsAny<CancellationToken>()))
            .Callback<ScheduledPost, CancellationToken>((p, _) => added = p);

        await _handler.Handle(Cmd(_item.Id, Ai1, 80, [new("geçmiş", Now.AddHours(-1))]), CancellationToken.None);

        added.ShouldNotBeNull();
        added.ScheduledAtUtc.ShouldBe(Now.AddMinutes(5));
        added.WasTimeAdjusted.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenTimeBeyondDayEnd_ShouldDropThatPostOnly()
    {
        await _handler.Handle(
            Cmd(_item.Id, Ai1, 80, [new("geçerli", Now.AddHours(2)), new("taşan", Now.AddHours(12))]),
            CancellationToken.None); // Now+12h = 2026-07-06T00:00Z > 21:00Z pencere sonu

        _item.Status.ShouldBe(ScheduleRunItemStatus.Completed);
        _item.DroppedPostCount.ShouldBe(1);
        _postRepo.Verify(p => p.AddAsync(It.IsAny<ScheduledPost>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAllPostsDropped_ShouldFailItem()
    {
        await _handler.Handle(
            Cmd(_item.Id, Ai1, 80, [new("taşan", Now.AddHours(12))]), CancellationToken.None);
        _item.Status.ShouldBe(ScheduleRunItemStatus.Failed);
    }

    [Fact]
    public async Task Handle_WhenErrorDetailPresent_ShouldMarkItemFailed()
    {
        var result = await _handler.Handle(
            Cmd(_item.Id, Ai1, 0, [], error: "provider 500"), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        _item.Status.ShouldBe(ScheduleRunItemStatus.Failed);
        _item.ErrorDetail.ShouldBe("provider 500");
    }

    [Fact]
    public async Task Handle_WhenMorePostsThanSnapshotMax_ShouldTakeOnlyMax()
    {
        var posts = Enumerable.Range(1, 5)
            .Select(i => new SubmittedScheduledPost($"p{i}", Now.AddHours(i)))
            .ToList();
        await _handler.Handle(Cmd(_item.Id, Ai1, 80, posts), CancellationToken.None);
        _postRepo.Verify(p => p.AddAsync(It.IsAny<ScheduledPost>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_WhenLastItemCompletes_ShouldCompleteRun()
    {
        await _handler.Handle(Cmd(_item.Id, Ai1, 80, [new("tek", Now.AddHours(2))]), CancellationToken.None);
        _run.Status.ShouldBe(ScheduleRunStatus.Completed);
    }
}

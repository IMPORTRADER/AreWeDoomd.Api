using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.PostScheduling.Commands.ProcessDueScheduledPosts;
using AreWeDoomd.Domain.Posts;
using AreWeDoomd.Domain.Scheduling;
using AreWeDoomd.UnitTests.Application;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.PostScheduling.Commands.ProcessDueScheduledPosts;

public sealed class ProcessDueScheduledPostsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ai1 = Guid.NewGuid();

    private readonly Mock<IScheduledPostRepository> _spRepo = new();
    private readonly Mock<IPostRepository> _postRepo = new();
    private readonly Mock<ISchedulingSettingsRepository> _settingsRepo = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly ProcessDueScheduledPostsCommandHandler _handler;

    public ProcessDueScheduledPostsCommandHandlerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _settingsRepo.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SchedulingSettings.CreateDefault(Now)); // LatePolicy=Expire, grace=3h
        _spRepo.Setup(r => r.GetStuckPublishingAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _spRepo.Setup(r => r.GetDuePendingAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _spRepo.Setup(r => r.TryClaimAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _postRepo.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);
        _handler = new ProcessDueScheduledPostsCommandHandler(
            _spRepo.Object, _postRepo.Object, _settingsRepo.Object, _clock.Object, _uow.Object, new StubAgentOpsLogger());
    }

    private static ScheduledPost DuePost(DateTimeOffset scheduledAt)
    {
        var post = ScheduledPost.Create(Guid.NewGuid(), Ai1, "içerik", scheduledAt, false, scheduledAt.AddHours(-1));
        return post;
    }

    [Fact]
    public async Task Handle_WhenDuePostWithinGrace_ShouldClaimCreatePostAndMarkPublished()
    {
        var due = DuePost(Now.AddMinutes(-10));
        _spRepo.Setup(r => r.GetDuePendingAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync([due]);
        _spRepo.Setup(r => r.GetByIdAsync(due.Id, It.IsAny<CancellationToken>())).ReturnsAsync(due);

        var result = await _handler.Handle(new ProcessDueScheduledPostsCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _postRepo.Verify(p => p.AddAsync(It.Is<Post>(x => x.UserId == Ai1 && x.Content == "içerik"),
            It.IsAny<CancellationToken>()), Times.Once);
        due.Status.ShouldBe(ScheduledPostStatus.Published);
    }

    [Fact]
    public async Task Handle_WhenClaimLost_ShouldSkipWithoutPublishing()
    {
        var due = DuePost(Now.AddMinutes(-10));
        _spRepo.Setup(r => r.GetDuePendingAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync([due]);
        _spRepo.Setup(r => r.TryClaimAsync(due.Id, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _spRepo.Setup(r => r.WasClaimWonAsync(due.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _handler.Handle(new ProcessDueScheduledPostsCommand(), CancellationToken.None);

        _postRepo.Verify(p => p.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLateBeyondGraceAndPolicyExpire_ShouldExpire()
    {
        var due = DuePost(Now.AddHours(-4)); // grace 3h aşıldı
        _spRepo.Setup(r => r.GetDuePendingAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync([due]);

        await _handler.Handle(new ProcessDueScheduledPostsCommand(), CancellationToken.None);

        due.Status.ShouldBe(ScheduledPostStatus.Expired);
        _postRepo.Verify(p => p.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLateBeyondGraceAndPolicyPublishAnyway_ShouldStillPublish()
    {
        var settings = SchedulingSettings.CreateDefault(Now);
        settings.Update(60, 3, 500, SchedulingLatePolicy.PublishAnyway, 3, LlmSchedulingStrategy.TwoStage, Now);
        _settingsRepo.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);

        var due = DuePost(Now.AddHours(-4));
        _spRepo.Setup(r => r.GetDuePendingAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync([due]);
        _spRepo.Setup(r => r.GetByIdAsync(due.Id, It.IsAny<CancellationToken>())).ReturnsAsync(due);

        await _handler.Handle(new ProcessDueScheduledPostsCommand(), CancellationToken.None);

        due.Status.ShouldBe(ScheduledPostStatus.Published);
    }

    [Fact]
    public async Task Handle_WhenStuckPublishingAndPostExists_ShouldCompleteToPublished()
    {
        var stuck = DuePost(Now.AddMinutes(-30));
        stuck.BeginPublishing(Guid.NewGuid(), Guid.NewGuid());
        _spRepo.Setup(r => r.GetStuckPublishingAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([stuck]);
        _postRepo.Setup(p => p.GetByIdAsync(stuck.PublishedPostId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Post.CreateWithId(stuck.PublishedPostId!.Value, Ai1, "içerik", Now));

        await _handler.Handle(new ProcessDueScheduledPostsCommand(), CancellationToken.None);

        stuck.Status.ShouldBe(ScheduledPostStatus.Published);
        _postRepo.Verify(p => p.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNextPendingDueTime()
    {
        var next = Now.AddHours(3);
        _spRepo.Setup(r => r.GetNextPendingDueUtcAsync(It.IsAny<CancellationToken>())).ReturnsAsync(next);

        var result = await _handler.Handle(new ProcessDueScheduledPostsCommand(), CancellationToken.None);

        result.Value.ShouldBe(next);
    }
}

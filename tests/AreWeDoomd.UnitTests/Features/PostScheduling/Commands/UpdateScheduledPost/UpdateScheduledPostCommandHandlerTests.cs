using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateScheduledPost;
using AreWeDoomd.Domain.Scheduling;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.PostScheduling.Commands.UpdateScheduledPost;

public sealed class UpdateScheduledPostCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<IScheduledPostRepository> _repo = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISchedulePublisherWaker> _waker = new();
    private readonly UpdateScheduledPostCommandHandler _handler;

    public UpdateScheduledPostCommandHandlerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _handler = new UpdateScheduledPostCommandHandler(_repo.Object, _clock.Object, _uow.Object, _waker.Object);
    }

    [Fact]
    public async Task Handle_WhenPendingAndTimeInFuture_ShouldUpdate()
    {
        var post = ScheduledPost.Create(Guid.NewGuid(), Guid.NewGuid(), "eski", Now.AddHours(2), false, Now);
        _repo.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var result = await _handler.Handle(
            new UpdateScheduledPostCommand(post.Id, "yeni", Now.AddHours(4)), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        post.Content.ShouldBe("yeni");
        _waker.Verify(w => w.Wake(), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTimeInPast_ShouldReturnValidationFailure()
    {
        var post = ScheduledPost.Create(Guid.NewGuid(), Guid.NewGuid(), "eski", Now.AddHours(2), false, Now);
        _repo.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var result = await _handler.Handle(
            new UpdateScheduledPostCommand(post.Id, "yeni", Now.AddMinutes(-5)), CancellationToken.None);

        result.ErrorType.ShouldBe(ErrorType.Failure);
    }

    [Fact]
    public async Task Handle_WhenNotPending_ShouldReturnConflict()
    {
        var post = ScheduledPost.Create(Guid.NewGuid(), Guid.NewGuid(), "eski", Now.AddHours(2), false, Now);
        post.Cancel();
        _repo.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var result = await _handler.Handle(
            new UpdateScheduledPostCommand(post.Id, "yeni", Now.AddHours(4)), CancellationToken.None);

        result.ErrorType.ShouldBe(ErrorType.Conflict);
    }
}

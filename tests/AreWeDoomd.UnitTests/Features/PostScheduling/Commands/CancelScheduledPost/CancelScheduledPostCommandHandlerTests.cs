using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Commands.CancelScheduledPost;
using AreWeDoomd.Domain.Scheduling;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.PostScheduling.Commands.CancelScheduledPost;

public sealed class CancelScheduledPostCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<IScheduledPostRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly CancelScheduledPostCommandHandler _handler;

    public CancelScheduledPostCommandHandlerTests()
    {
        _handler = new CancelScheduledPostCommandHandler(_repo.Object, _uow.Object);
    }

    [Fact]
    public async Task Handle_WhenPending_ShouldCancel()
    {
        var post = ScheduledPost.Create(Guid.NewGuid(), Guid.NewGuid(), "içerik", Now.AddHours(2), false, Now);
        _repo.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var result = await _handler.Handle(new CancelScheduledPostCommand(post.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        post.Status.ShouldBe(ScheduledPostStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_WhenNotPending_ShouldReturnConflict()
    {
        var post = ScheduledPost.Create(Guid.NewGuid(), Guid.NewGuid(), "içerik", Now.AddHours(2), false, Now);
        post.BeginPublishing(Guid.NewGuid(), Guid.NewGuid());
        _repo.Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var result = await _handler.Handle(new CancelScheduledPostCommand(post.Id), CancellationToken.None);

        result.ErrorType.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldReturnNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledPost?)null);

        var result = await _handler.Handle(new CancelScheduledPostCommand(Guid.NewGuid()), CancellationToken.None);

        result.ErrorType.ShouldBe(ErrorType.NotFound);
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.CommentLikes.Commands.UnlikeComment;
using AreWeDoomd.Domain.Comments;
using AreWeDoomd.Domain.Posts;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.CommentLikes.Commands.UnlikeComment;

public sealed class UnlikeCommentCommandHandlerTests
{
    private static readonly Guid CommentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IPostRepository> _postRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UnlikeCommentCommandHandler _handler;

    public UnlikeCommentCommandHandlerTests()
    {
        _handler = new UnlikeCommentCommandHandler(
            _postRepositoryMock.Object,
            _commentRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCommentUnliked_ShouldDecrementPostCommentLikeCount()
    {
        var post = Post.Create(Guid.NewGuid(), "content", Now);
        post.IncrementCommentLikeCount();
        var comment = Comment.Create(post.Id, Guid.NewGuid(), "a comment", Now);
        comment.Like(UserId, Now);

        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepositoryMock
            .Setup(r => r.GetByIdWithLikesAsync(post.Id, CommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var result = await _handler.Handle(
            new UnlikeCommentCommand(post.Id, CommentId, UserId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        post.CommentLikeCount.ShouldBe(0);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotPreviouslyLiked_ShouldNotDecrementPostCommentLikeCount()
    {
        var post = Post.Create(Guid.NewGuid(), "content", Now);
        post.IncrementCommentLikeCount();
        var comment = Comment.Create(post.Id, Guid.NewGuid(), "a comment", Now);

        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepositoryMock
            .Setup(r => r.GetByIdWithLikesAsync(post.Id, CommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var result = await _handler.Handle(
            new UnlikeCommentCommand(post.Id, CommentId, UserId), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        post.CommentLikeCount.ShouldBe(1);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

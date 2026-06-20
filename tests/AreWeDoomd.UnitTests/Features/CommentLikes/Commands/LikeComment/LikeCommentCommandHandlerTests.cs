using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.CommentLikes.Commands.LikeComment;
using AreWeDoomd.Domain.Comments;
using AreWeDoomd.Domain.Posts;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.CommentLikes.Commands.LikeComment;

public sealed class LikeCommentCommandHandlerTests
{
    private static readonly Guid CommentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IPostRepository> _postRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly LikeCommentCommandHandler _handler;

    public LikeCommentCommandHandlerTests()
    {
        _dateTimeProviderMock.SetupGet(p => p.UtcNow).Returns(Now);
        _handler = new LikeCommentCommandHandler(
            _postRepositoryMock.Object,
            _commentRepositoryMock.Object,
            _dateTimeProviderMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCommentLiked_ShouldIncrementPostCommentLikeCount()
    {
        var post = Post.Create(Guid.NewGuid(), "content", Now);
        var comment = Comment.Create(post.Id, Guid.NewGuid(), "a comment", Now);

        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepositoryMock
            .Setup(r => r.GetByIdWithLikesAsync(post.Id, CommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var result = await _handler.Handle(
            new LikeCommentCommand(post.Id, CommentId, UserId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        post.CommentLikeCount.ShouldBe(1);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyLiked_ShouldNotIncrementPostCommentLikeCount()
    {
        var post = Post.Create(Guid.NewGuid(), "content", Now);
        var comment = Comment.Create(post.Id, Guid.NewGuid(), "a comment", Now);
        comment.Like(UserId, Now);

        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(post.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepositoryMock
            .Setup(r => r.GetByIdWithLikesAsync(post.Id, CommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var result = await _handler.Handle(
            new LikeCommentCommand(post.Id, CommentId, UserId), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        post.CommentLikeCount.ShouldBe(0);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

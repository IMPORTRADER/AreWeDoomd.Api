using AreWeDoomd.Domain.Posts;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Posts;

public sealed class PostCommentLikeCountTests
{
    private static Post CreatePost()
    {
        return Post.Create(Guid.NewGuid(), "content", DateTimeOffset.UtcNow);
    }

    [Fact]
    public void IncrementCommentLikeCount_WhenCalled_ShouldIncreaseByOne()
    {
        var post = CreatePost();

        post.IncrementCommentLikeCount();

        post.CommentLikeCount.ShouldBe(1);
    }

    [Fact]
    public void DecrementCommentLikeCount_WhenPositive_ShouldDecreaseByOne()
    {
        var post = CreatePost();
        post.IncrementCommentLikeCount();
        post.IncrementCommentLikeCount();

        post.DecrementCommentLikeCount();

        post.CommentLikeCount.ShouldBe(1);
    }

    [Fact]
    public void DecrementCommentLikeCount_WhenZero_ShouldStayZero()
    {
        var post = CreatePost();

        post.DecrementCommentLikeCount();

        post.CommentLikeCount.ShouldBe(0);
    }

    [Fact]
    public void RemoveComment_WhenCommentHadLikes_ShouldReduceCommentLikeCountByThatAmount()
    {
        var post = CreatePost();
        var comment = post.AddComment(Guid.NewGuid(), "a comment", DateTimeOffset.UtcNow);
        comment.Like(Guid.NewGuid(), DateTimeOffset.UtcNow);
        comment.Like(Guid.NewGuid(), DateTimeOffset.UtcNow);
        post.IncrementCommentLikeCount();
        post.IncrementCommentLikeCount();

        post.RemoveComment(comment.Id);

        post.CommentLikeCount.ShouldBe(0);
    }

    [Fact]
    public void RemoveComment_WhenOtherCommentsRetainLikes_ShouldOnlySubtractRemovedCommentsLikes()
    {
        var post = CreatePost();
        var kept = post.AddComment(Guid.NewGuid(), "kept", DateTimeOffset.UtcNow);
        var removed = post.AddComment(Guid.NewGuid(), "removed", DateTimeOffset.UtcNow);
        kept.Like(Guid.NewGuid(), DateTimeOffset.UtcNow);
        removed.Like(Guid.NewGuid(), DateTimeOffset.UtcNow);
        removed.Like(Guid.NewGuid(), DateTimeOffset.UtcNow);
        // Post rollup = 3 likes total (1 kept + 2 removed), as the handler would have incremented.
        post.IncrementCommentLikeCount();
        post.IncrementCommentLikeCount();
        post.IncrementCommentLikeCount();

        post.RemoveComment(removed.Id);

        post.CommentLikeCount.ShouldBe(1);
    }

    [Fact]
    public void RemoveComment_WhenCommentHadNoLikes_ShouldLeaveCommentLikeCountUnchanged()
    {
        var post = CreatePost();
        var liked = post.AddComment(Guid.NewGuid(), "liked", DateTimeOffset.UtcNow);
        var unliked = post.AddComment(Guid.NewGuid(), "unliked", DateTimeOffset.UtcNow);
        liked.Like(Guid.NewGuid(), DateTimeOffset.UtcNow);
        post.IncrementCommentLikeCount();

        post.RemoveComment(unliked.Id);

        post.CommentLikeCount.ShouldBe(1);
    }
}

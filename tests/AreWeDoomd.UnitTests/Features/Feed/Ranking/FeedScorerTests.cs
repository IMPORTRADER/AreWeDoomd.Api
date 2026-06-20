using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Common;
using AreWeDoomd.Application.Features.Feed.Common;
using AreWeDoomd.Application.Features.Feed.Ranking;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Feed.Ranking;

public sealed class FeedScorerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

    private static FeedPostResult Post(
        Guid id,
        DateTimeOffset createdAt,
        int likeCount = 0,
        int commentCount = 0,
        int commentLikeCount = 0)
    {
        return new FeedPostResult(
            id,
            new PostAuthorResult(Guid.NewGuid(), "user", "Human", null),
            "content",
            likeCount,
            commentCount,
            commentLikeCount,
            Array.Empty<CommentResult>(),
            createdAt,
            null);
    }

    [Fact]
    public void Rank_WhenSameAge_ShouldOrderByEngagementDescending()
    {
        var createdAt = Now.AddHours(-1);
        var low = Post(Guid.NewGuid(), createdAt, likeCount: 1);
        var high = Post(Guid.NewGuid(), createdAt, likeCount: 50);

        var ranked = FeedScorer.Rank(new[] { low, high }, Now);

        ranked[0].Id.ShouldBe(high.Id);
        ranked[1].Id.ShouldBe(low.Id);
    }

    [Fact]
    public void Rank_WhenZeroEngagement_ShouldOrderByNewestFirst()
    {
        var older = Post(Guid.NewGuid(), Now.AddHours(-10));
        var newer = Post(Guid.NewGuid(), Now.AddHours(-1));

        var ranked = FeedScorer.Rank(new[] { older, newer }, Now);

        ranked[0].Id.ShouldBe(newer.Id);
        ranked[1].Id.ShouldBe(older.Id);
    }

    [Fact]
    public void Rank_WhenOldPostHighlyEngaged_ShouldDecayBelowFreshLowEngagement()
    {
        var oldViral = Post(Guid.NewGuid(), Now.AddDays(-7), likeCount: 100);
        var freshActive = Post(Guid.NewGuid(), Now.AddHours(-1), commentCount: 5);

        var ranked = FeedScorer.Rank(new[] { oldViral, freshActive }, Now);

        ranked[0].Id.ShouldBe(freshActive.Id);
    }

    [Fact]
    public void Rank_WhenComparingSignals_ShouldWeightCommentAboveLikeAboveCommentLike()
    {
        var createdAt = Now.AddHours(-1);
        var byComment = Post(Guid.NewGuid(), createdAt, commentCount: 10);
        var byLike = Post(Guid.NewGuid(), createdAt, likeCount: 10);
        var byCommentLike = Post(Guid.NewGuid(), createdAt, commentLikeCount: 10);

        var ranked = FeedScorer.Rank(new[] { byLike, byCommentLike, byComment }, Now);

        ranked[0].Id.ShouldBe(byComment.Id);
        ranked[1].Id.ShouldBe(byLike.Id);
        ranked[2].Id.ShouldBe(byCommentLike.Id);
    }

    [Fact]
    public void Rank_WhenScoreAndCreatedAtTie_ShouldBreakByIdAscending()
    {
        var createdAt = Now.AddHours(-1);
        var a = Post(new Guid("00000000-0000-0000-0000-000000000001"), createdAt);
        var b = Post(new Guid("00000000-0000-0000-0000-000000000002"), createdAt);

        var ranked = FeedScorer.Rank(new[] { b, a }, Now);

        ranked[0].Id.ShouldBe(a.Id);
        ranked[1].Id.ShouldBe(b.Id);
    }

    [Fact]
    public void Rank_WhenEmpty_ShouldReturnEmpty()
    {
        var ranked = FeedScorer.Rank(Array.Empty<FeedPostResult>(), Now);

        ranked.ShouldBeEmpty();
    }
}

using AreWeDoomd.Application.Features.Feed.Common;

namespace AreWeDoomd.Application.Features.Feed.Ranking;

public static class FeedScorer
{
    private const double CommentWeight = 3.0;
    private const double LikeWeight = 1.0;
    private const double CommentLikeWeight = 0.5;
    private const double Gravity = 1.5;
    private const double BaseScore = 1.0;
    private const double AgeOffsetHours = 2.0;

    public static IReadOnlyList<FeedPostResult> Rank(
        IReadOnlyList<FeedPostResult> posts,
        DateTimeOffset now)
    {
        return posts
            .OrderByDescending(post => Score(post, now))
            .ThenByDescending(post => post.CreatedAt)
            .ThenBy(post => post.Id)
            .ToList();
    }

    private static double Score(FeedPostResult post, DateTimeOffset now)
    {
        var ageHours = Math.Max(0.0, (now - post.CreatedAt).TotalHours);

        var engagement = BaseScore
            + (CommentWeight * post.CommentCount)
            + (LikeWeight * post.LikeCount)
            + (CommentLikeWeight * post.CommentLikeCount);

        return engagement / Math.Pow(ageHours + AgeOffsetHours, Gravity);
    }
}

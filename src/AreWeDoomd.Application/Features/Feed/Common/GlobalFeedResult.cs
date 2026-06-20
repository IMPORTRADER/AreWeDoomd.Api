namespace AreWeDoomd.Application.Features.Feed.Common;

public sealed record GlobalFeedResult(
    IReadOnlyList<FeedPostResult> Posts,
    DateTimeOffset AsOf,
    bool HasMore);

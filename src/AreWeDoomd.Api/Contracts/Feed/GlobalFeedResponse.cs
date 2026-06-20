namespace AreWeDoomd.Api.Contracts.Feed;

public sealed record GlobalFeedResponse(
    IReadOnlyList<FeedPostResponse> Items,
    DateTimeOffset AsOf,
    bool HasMore);

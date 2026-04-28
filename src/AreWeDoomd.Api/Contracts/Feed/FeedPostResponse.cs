using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Common;

namespace AreWeDoomd.Api.Contracts.Feed;

public sealed record FeedPostResponse(
    Guid Id,
    PostAuthor Author,
    string Content,
    int LikeCount,
    int CommentCount,
    IReadOnlyList<CommentResponse> Comments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

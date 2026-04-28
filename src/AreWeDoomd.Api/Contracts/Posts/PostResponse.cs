using AreWeDoomd.Api.Contracts.Common;

namespace AreWeDoomd.Api.Contracts.Posts;

public sealed record PostResponse(
    Guid Id,
    PostAuthor Author,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

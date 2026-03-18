namespace AreWeDoomd.Application.Features.Posts.Common;

public sealed record PostResult(
    Guid Id,
    Guid UserId,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

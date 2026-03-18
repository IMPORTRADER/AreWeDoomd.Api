namespace AreWeDoomd.Api.Contracts.Posts;

public sealed record PostResponse(
    Guid Id,
    Guid UserId,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

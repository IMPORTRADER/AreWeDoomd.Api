namespace AreWeDoomd.Api.Contracts.Comments;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid UserId,
    string Content,
    int LikeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

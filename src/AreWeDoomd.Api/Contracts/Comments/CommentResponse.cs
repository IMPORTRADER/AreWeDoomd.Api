namespace AreWeDoomd.Api.Contracts.Comments;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid UserId,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

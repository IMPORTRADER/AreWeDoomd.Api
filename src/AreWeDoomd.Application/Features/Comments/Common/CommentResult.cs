namespace AreWeDoomd.Application.Features.Comments.Common;

public sealed record CommentResult(
    Guid Id,
    Guid PostId,
    Guid UserId,
    string Content,
    int LikeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

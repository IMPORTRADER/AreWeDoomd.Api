using AreWeDoomd.Api.Contracts.Common;

namespace AreWeDoomd.Api.Contracts.Comments;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    PostAuthor Author,
    string Content,
    int LikeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

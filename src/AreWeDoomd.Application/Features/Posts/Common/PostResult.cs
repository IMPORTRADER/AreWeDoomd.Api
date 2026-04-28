using AreWeDoomd.Application.Features.Common;

namespace AreWeDoomd.Application.Features.Posts.Common;

public sealed record PostResult(
    Guid Id,
    PostAuthorResult Author,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

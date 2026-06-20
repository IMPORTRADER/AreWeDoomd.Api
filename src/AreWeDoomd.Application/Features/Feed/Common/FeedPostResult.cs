using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Common;

namespace AreWeDoomd.Application.Features.Feed.Common;

public sealed record FeedPostResult(
    Guid Id,
    PostAuthorResult Author,
    string Content,
    int LikeCount,
    int CommentCount,
    int CommentLikeCount,
    IReadOnlyList<CommentResult> Comments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

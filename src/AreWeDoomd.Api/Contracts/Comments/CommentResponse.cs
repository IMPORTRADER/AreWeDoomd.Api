using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Filters;

namespace AreWeDoomd.Api.Contracts.Comments;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    PostAuthor Author,
    string Content,
    int LikeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
    : IActivityObjectCarrier
{
    string IActivityObjectCarrier.ActivityObjectId => Id.ToString();
    ActivityObjectType IActivityObjectCarrier.ActivityObjectType => ActivityObjectType.Comment;
    string? IActivityObjectCarrier.ActivityObjectTextPreview => Content;
}

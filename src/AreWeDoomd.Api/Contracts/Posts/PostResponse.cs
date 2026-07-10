using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Filters;

namespace AreWeDoomd.Api.Contracts.Posts;

public sealed record PostResponse(
    Guid Id,
    PostAuthor Author,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
    : IActivityObjectCarrier
{
    string IActivityObjectCarrier.ActivityObjectId => Id.ToString();
    ActivityObjectType IActivityObjectCarrier.ActivityObjectType => ActivityObjectType.Post;
    string? IActivityObjectCarrier.ActivityObjectTextPreview => Content;
}

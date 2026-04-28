namespace AreWeDoomd.Application.Features.CommentLikes.Common;

public sealed record CommentLikeUserResult(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl,
    DateTimeOffset LikedAt);

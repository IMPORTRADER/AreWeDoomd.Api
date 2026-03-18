namespace AreWeDoomd.Application.Features.PostLikes.Common;

public sealed record PostLikeUserResult(
    Guid UserId,
    string Username,
    string? ProfileImageUrl,
    DateTimeOffset LikedAt);

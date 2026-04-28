namespace AreWeDoomd.Api.Contracts.PostLikes;

public sealed record PostLikeUserResponse(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl,
    DateTimeOffset LikedAt);

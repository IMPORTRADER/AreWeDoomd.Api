namespace AreWeDoomd.Api.Contracts.CommentLikes;

public sealed record CommentLikeUserResponse(
    Guid UserId,
    string Username,
    string? ProfileImageUrl,
    DateTimeOffset LikedAt);

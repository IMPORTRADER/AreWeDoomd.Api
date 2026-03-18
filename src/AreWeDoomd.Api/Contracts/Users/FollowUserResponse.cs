namespace AreWeDoomd.Api.Contracts.Users;

public sealed record FollowUserResponse(
    Guid UserId,
    string Username,
    string? ProfileImageUrl,
    DateTimeOffset FollowedAt);

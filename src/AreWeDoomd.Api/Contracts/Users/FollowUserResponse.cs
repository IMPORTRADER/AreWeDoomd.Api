namespace AreWeDoomd.Api.Contracts.Users;

public sealed record FollowUserResponse(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl,
    DateTimeOffset FollowedAt);

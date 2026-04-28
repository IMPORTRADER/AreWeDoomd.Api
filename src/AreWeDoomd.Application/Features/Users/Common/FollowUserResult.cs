namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record FollowUserResult(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl,
    DateTimeOffset FollowedAt);

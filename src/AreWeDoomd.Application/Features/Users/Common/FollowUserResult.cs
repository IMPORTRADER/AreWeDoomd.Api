namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record FollowUserResult(
    Guid UserId,
    string Username,
    string? ProfileImageUrl,
    DateTimeOffset FollowedAt);

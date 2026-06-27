namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record UserProfileDetailResult(
    Guid UserId,
    string Username,
    string UserType,
    string? Bio,
    string? ProfileImageUrl,
    DateTimeOffset JoinedAt,
    ProfileStatsResult Stats,
    IReadOnlyList<ProfileBadgeResult> Badges,
    bool IsFollowedByMe,
    bool IsMe);

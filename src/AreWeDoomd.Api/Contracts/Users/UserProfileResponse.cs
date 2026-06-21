namespace AreWeDoomd.Api.Contracts.Users;

public sealed record UserProfileResponse(
    Guid UserId,
    string Username,
    string UserType,
    string? Bio,
    string? ProfileImageUrl,
    DateTimeOffset JoinedAt,
    ProfileStatsResponse Stats,
    IReadOnlyList<ProfileBadgeResponse> Badges,
    bool IsFollowedByMe,
    bool IsMe);

namespace AreWeDoomd.Api.Contracts.Users;

public sealed record UserSummaryResponse(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl,
    string? Bio,
    bool IsFollowedByMe);

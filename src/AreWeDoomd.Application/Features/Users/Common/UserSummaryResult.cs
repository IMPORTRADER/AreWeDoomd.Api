namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record UserSummaryResult(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl,
    string? Bio,
    bool IsFollowedByMe);

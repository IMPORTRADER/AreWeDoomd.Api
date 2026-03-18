namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record UserProfileResult(
    Guid UserId,
    string Username,
    string Email,
    string UserType,
    string? ProfileImageUrl,
    string? Biography);

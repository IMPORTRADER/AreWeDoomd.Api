namespace AreWeDoomd.Api.Contracts.Users;

public sealed record UserProfileResponse(
    Guid UserId,
    string Username,
    string Email,
    string UserType,
    string? ProfileImageUrl,
    string? Biography);

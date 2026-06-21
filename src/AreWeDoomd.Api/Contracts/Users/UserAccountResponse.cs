namespace AreWeDoomd.Api.Contracts.Users;

public sealed record UserAccountResponse(
    Guid UserId,
    string Username,
    string Email,
    string UserType,
    string? ProfileImageUrl,
    string? Biography);

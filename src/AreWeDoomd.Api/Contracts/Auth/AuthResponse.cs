namespace AreWeDoomd.Api.Contracts.Auth;

public sealed record AuthResponse(
    Guid UserId,
    string Username,
    string Email,
    string UserType,
    string AccessToken,
    DateTimeOffset ExpiresAt);


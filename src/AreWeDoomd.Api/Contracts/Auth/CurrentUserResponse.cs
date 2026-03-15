namespace AreWeDoomd.Api.Contracts.Auth;

public sealed record CurrentUserResponse(
    Guid UserId,
    string Username,
    string Email,
    string UserType);

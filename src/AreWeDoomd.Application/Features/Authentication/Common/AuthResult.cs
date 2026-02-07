using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Features.Authentication.Common;

public sealed record AuthResult(
    Guid UserId,
    string Username,
    string Email,
    UserType UserType,
    string AccessToken,
    DateTimeOffset ExpiresAt);


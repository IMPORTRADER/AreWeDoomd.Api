using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Models;

public sealed record ClientContext(
    string ClientId,
    UserType UserType,
    string Issuer,
    string Audience,
    string TokenId,
    DateTimeOffset ExpiresAt);


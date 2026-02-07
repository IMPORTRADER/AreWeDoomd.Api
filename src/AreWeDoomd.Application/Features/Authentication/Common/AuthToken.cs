namespace AreWeDoomd.Application.Features.Authentication.Common;

public sealed record AuthToken(string AccessToken, DateTimeOffset ExpiresAt);


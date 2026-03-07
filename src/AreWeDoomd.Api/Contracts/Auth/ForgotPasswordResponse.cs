namespace AreWeDoomd.Api.Contracts.Auth;

public sealed record ForgotPasswordResponse(string ResetCode, DateTimeOffset ExpiresAt);


namespace AreWeDoomd.Application.Features.Authentication.Common;

public sealed record ForgotPasswordResult(string ResetCode, DateTimeOffset ExpiresAt);


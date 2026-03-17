namespace AreWeDoomd.Api.Contracts.Auth;

public sealed record ResetPasswordRequest(string Username, string Code, string NewPassword);


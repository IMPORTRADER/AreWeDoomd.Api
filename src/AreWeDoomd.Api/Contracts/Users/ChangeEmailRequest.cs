namespace AreWeDoomd.Api.Contracts.Users;

public sealed record ChangeEmailRequest(
    string CurrentPassword,
    string NewEmail);

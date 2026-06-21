namespace AreWeDoomd.Api.Contracts.Users;

public sealed record UpdateUserProfileRequest(
    string? Username,
    string? Email,
    string? Bio);

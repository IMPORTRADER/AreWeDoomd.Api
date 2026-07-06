namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record CreateAiUserRequest(
    string Username,
    string? Email,
    IReadOnlyList<string> Traits,
    string TypingStyle,
    string Summary);

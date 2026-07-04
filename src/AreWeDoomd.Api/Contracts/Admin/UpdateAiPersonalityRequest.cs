namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record UpdateAiPersonalityRequest(
    IReadOnlyList<string> Traits,
    string TypingStyle,
    string Summary);

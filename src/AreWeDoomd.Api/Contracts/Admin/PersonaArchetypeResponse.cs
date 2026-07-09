namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record PersonaArchetypeResponse(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> UsernamePatterns,
    IReadOnlyList<string> Traits,
    IReadOnlyList<string> TypingStyles,
    IReadOnlyList<string> Summaries);

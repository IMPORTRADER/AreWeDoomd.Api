namespace AreWeDoomd.Application.Common.Models;

public sealed record PersonaArchetype(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> UsernamePatterns,
    IReadOnlyList<string> Traits,
    IReadOnlyList<string> TypingStyles,
    IReadOnlyList<string> Summaries);

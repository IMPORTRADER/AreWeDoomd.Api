namespace AreWeDoomd.Application.Common.Models;

public sealed record GeneratedPersona(
    string Username,
    IReadOnlyList<string> Traits,
    string TypingStyle,
    string Summary);

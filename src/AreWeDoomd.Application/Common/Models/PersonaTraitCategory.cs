namespace AreWeDoomd.Application.Common.Models;

public sealed record PersonaTraitCategory(
    string Name,
    IReadOnlyList<string> Traits);

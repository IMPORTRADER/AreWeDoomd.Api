namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record PersonaTraitCategoryResponse(
    string Name,
    IReadOnlyList<string> Traits);

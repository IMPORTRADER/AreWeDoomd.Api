namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record PersonaCatalogResponse(
    IReadOnlyList<PersonaArchetypeResponse> Archetypes,
    IReadOnlyList<PersonaTraitCategoryResponse> TraitCategories,
    IReadOnlyList<string> TypingStyleSuggestions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> UsernameWordPools);

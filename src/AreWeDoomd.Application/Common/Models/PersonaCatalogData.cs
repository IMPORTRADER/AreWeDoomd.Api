namespace AreWeDoomd.Application.Common.Models;

public sealed record PersonaCatalogData(
    IReadOnlyList<PersonaArchetype> Archetypes,
    IReadOnlyList<PersonaTraitCategory> TraitCategories,
    IReadOnlyList<string> TypingStyleSuggestions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> UsernameWordPools);

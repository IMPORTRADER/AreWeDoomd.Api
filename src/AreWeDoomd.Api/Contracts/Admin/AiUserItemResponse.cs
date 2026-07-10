namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record AiUserItemResponse(
    Guid Id,
    string Username,
    string? ProfileImageUrl,
    DateTimeOffset CreatedAt,
    bool HasPersonality,
    IReadOnlyList<string> Traits,
    string? TypingStyle,
    int? PersonaVersion,
    DateTimeOffset? DeactivatedAt);

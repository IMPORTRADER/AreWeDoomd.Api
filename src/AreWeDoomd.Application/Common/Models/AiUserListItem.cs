namespace AreWeDoomd.Application.Common.Models;

public sealed record AiUserListItem(
    Guid Id,
    string Username,
    string? ProfileImageUrl,
    DateTimeOffset CreatedAt,
    bool HasPersonality,
    IReadOnlyList<string> Traits,
    string? TypingStyle,
    int? PersonaVersion,
    DateTimeOffset? DeactivatedAt);

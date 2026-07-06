namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;

public sealed record AiUserDetailResult(
    Guid Id,
    string Username,
    string Email,
    string? ProfileImageUrl,
    string? Biography,
    DateTimeOffset CreatedAt,
    bool HasPersonality,
    IReadOnlyList<string> Traits,
    string? TypingStyle,
    string? Summary,
    int? PersonaVersion,
    DateTimeOffset? PersonaUpdatedAt);

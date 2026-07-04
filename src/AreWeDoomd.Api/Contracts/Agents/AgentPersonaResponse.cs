namespace AreWeDoomd.Api.Contracts.Agents;

public sealed record AgentPersonaResponse(
    Guid UserId,
    IReadOnlyList<string> Traits,
    string TypingStyle,
    string Summary,
    int Version);

namespace AreWeDoomd.Application.Features.Agents.Queries.GetAgentPersona;

public sealed record AgentPersonaResult(
    Guid UserId,
    IReadOnlyList<string> Traits,
    string TypingStyle,
    string Summary,
    int Version);

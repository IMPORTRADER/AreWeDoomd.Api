namespace AreWeDoomd.AgentService.Context.Wire;

internal sealed record ApiAgentPersonaResponse(
    Guid UserId,
    List<string> Traits,
    string TypingStyle,
    string Summary,
    int Version);

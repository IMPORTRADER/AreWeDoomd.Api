namespace AreWeDoomd.AgentService.Decisions;

public sealed record AgentDecision(
    AgentAction Action,
    string? Content,
    string? Reasoning);

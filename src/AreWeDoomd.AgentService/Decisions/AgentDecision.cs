namespace AreWeDoomd.AgentService.Decisions;

public sealed record AgentDecision(
    IReadOnlyList<AgentActionDecision> Actions,
    string? Reasoning);

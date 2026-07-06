namespace AreWeDoomd.AgentService.Decisions;

public sealed record LlmDecisionResult(
    AgentDecision Decision,
    int Attempts,
    LlmDecisionSource Source,
    string? ErrorDetail,
    string? SessionLogRef);

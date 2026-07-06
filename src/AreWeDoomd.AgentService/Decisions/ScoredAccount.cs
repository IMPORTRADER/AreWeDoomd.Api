namespace AreWeDoomd.AgentService.Decisions;

public sealed record ScoredAccount(Guid RunItemId, string Reasoning, int DesireScore, int HypotheticalPostCount);

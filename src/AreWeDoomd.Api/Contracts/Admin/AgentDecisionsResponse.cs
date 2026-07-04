namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record AgentDecisionsResponse(
    IReadOnlyList<AgentDecisionItemResponse> Items,
    string? NextCursor,
    bool HasMore,
    bool LogAvailable);

namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record AgentOpsLogsResponse(
    IReadOnlyList<AgentOpsLogItemResponse> Items,
    string? NextCursor,
    bool HasMore,
    bool LogAvailable);

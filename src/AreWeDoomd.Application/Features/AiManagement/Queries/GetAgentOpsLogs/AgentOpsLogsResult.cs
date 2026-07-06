using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentOpsLogs;

public sealed record AgentOpsLogsResult(
    IReadOnlyList<AgentOpsLogRecord> Items,
    string? NextCursor,
    bool HasMore,
    bool LogAvailable);

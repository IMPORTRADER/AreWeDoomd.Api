namespace AreWeDoomd.Application.Common.Models;

public sealed record AgentOpsLogPage(
    IReadOnlyList<AgentOpsLogRecord> Items, string? NextCursor, bool HasMore, bool LogAvailable);

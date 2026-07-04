using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentDecisions;

public sealed record AgentDecisionsResult(
    IReadOnlyList<DecisionLogRecord> Items,
    string? NextCursor,
    bool HasMore,
    bool LogAvailable);

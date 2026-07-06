namespace AreWeDoomd.Application.Common.Models;

public sealed record DecisionLogPage(
    IReadOnlyList<DecisionLogRecord> Items, string? NextCursor, bool HasMore, bool LogAvailable);

using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IDecisionLogReader
{
    Task<DecisionLogPage> ReadAsync(DecisionLogFilter filter, string? cursor, int pageSize, CancellationToken ct);
    Task<DecisionLogDailyStats?> GetDailyStatsAsync(DateOnly dateUtc, DateTimeOffset nowUtc, CancellationToken ct);
}

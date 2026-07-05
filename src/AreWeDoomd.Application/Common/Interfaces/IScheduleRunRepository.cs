using AreWeDoomd.Domain.Scheduling;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IScheduleRunRepository
{
    Task AddAsync(ScheduleRun run, CancellationToken ct);
    Task<ScheduleRun?> GetWithItemsAsync(Guid runId, CancellationToken ct);
    Task<ScheduleRunItem?> GetItemAsync(Guid runItemId, CancellationToken ct);
    Task<ScheduleRun?> GetRunOfItemAsync(Guid runItemId, CancellationToken ct);
    Task<IReadOnlyList<ScheduleRunItem>> GetActiveItemsForDateAsync(DateOnly runDate, IReadOnlyList<Guid>? aiUserIds, CancellationToken ct);
    Task<IReadOnlyList<ScheduleRunItem>> GetStaleAwaitingItemsAsync(DateTimeOffset pushedBeforeUtc, CancellationToken ct);
    Task<IReadOnlyList<ScheduleRun>> ListByDateAsync(DateOnly runDate, int offset, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<ScheduleRun>> GetRunningRunsWithItemsAsync(CancellationToken ct);
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Scheduling;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class ScheduleRunRepository(AreWeDoomdDbContext dbContext) : IScheduleRunRepository
{
    public async Task AddAsync(ScheduleRun run, CancellationToken ct)
    {
        await dbContext.ScheduleRuns.AddAsync(run, ct);
    }

    public Task<ScheduleRun?> GetWithItemsAsync(Guid runId, CancellationToken ct)
    {
        return dbContext.ScheduleRuns
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
    }

    public Task<ScheduleRunItem?> GetItemAsync(Guid runItemId, CancellationToken ct)
    {
        return dbContext.ScheduleRunItems.FirstOrDefaultAsync(i => i.Id == runItemId, ct);
    }

    public Task<ScheduleRun?> GetRunOfItemAsync(Guid runItemId, CancellationToken ct)
    {
        return dbContext.ScheduleRuns
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Items.Any(i => i.Id == runItemId), ct);
    }

    public async Task<IReadOnlyList<ScheduleRunItem>> GetActiveItemsForDateAsync(
        DateOnly runDate, IReadOnlyList<Guid>? aiUserIds, CancellationToken ct)
    {
        var query = dbContext.ScheduleRunItems
            .Where(i => i.RunDate == runDate && i.Status != ScheduleRunItemStatus.Superseded);
        if (aiUserIds is not null)
        {
            query = query.Where(i => aiUserIds.Contains(i.AiUserId));
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduleRunItem>> GetStaleAwaitingItemsAsync(
        DateTimeOffset pushedBeforeUtc, CancellationToken ct)
    {
        return await dbContext.ScheduleRunItems
            .Where(i => i.Status == ScheduleRunItemStatus.AwaitingLlm && i.LastPushedAtUtc < pushedBeforeUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduleRun>> ListByDateAsync(
        DateOnly runDate, int offset, int pageSize, CancellationToken ct)
    {
        return await dbContext.ScheduleRuns
            .Include(r => r.Items)
            .Where(r => r.RunDate == runDate)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduleRun>> GetRunningRunsWithItemsAsync(CancellationToken ct)
    {
        return await dbContext.ScheduleRuns
            .Include(r => r.Items)
            .Where(r => r.Status == ScheduleRunStatus.Running)
            .ToListAsync(ct);
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Scheduling;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class ScheduledPostRepository(AreWeDoomdDbContext dbContext) : IScheduledPostRepository
{
    public async Task AddAsync(ScheduledPost post, CancellationToken ct)
    {
        await dbContext.ScheduledPosts.AddAsync(post, ct);
    }

    public Task<ScheduledPost?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return dbContext.ScheduledPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<ScheduledPost>> GetDuePendingAsync(DateTimeOffset nowUtc, CancellationToken ct)
    {
        return await dbContext.ScheduledPosts
            .Where(p => p.Status == ScheduledPostStatus.Pending && p.ScheduledAtUtc <= nowUtc)
            .OrderBy(p => p.ScheduledAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> TryClaimAsync(Guid id, Guid claimToken, Guid publishedPostId, CancellationToken ct)
    {
        int updated = await dbContext.ScheduledPosts
            .Where(p => p.Id == id && p.Status == ScheduledPostStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, ScheduledPostStatus.Publishing)
                .SetProperty(p => p.ClaimToken, claimToken)
                .SetProperty(p => p.PublishedPostId, publishedPostId)
                .SetProperty(p => p.AttemptCount, p => p.AttemptCount + 1), ct);
        return updated == 1;
    }

    public Task<bool> WasClaimWonAsync(Guid id, Guid claimToken, CancellationToken ct)
    {
        return dbContext.ScheduledPosts.AnyAsync(p => p.Id == id && p.ClaimToken == claimToken, ct);
    }

    public async Task<IReadOnlyList<ScheduledPost>> GetStuckPublishingAsync(
        DateTimeOffset claimedBeforeUtc, CancellationToken ct)
    {
        return await dbContext.ScheduledPosts
            .Where(p => p.Status == ScheduledPostStatus.Publishing && p.ScheduledAtUtc < claimedBeforeUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduledPost>> GetPendingByRunItemIdsAsync(
        IReadOnlyList<Guid> runItemIds, CancellationToken ct)
    {
        return await dbContext.ScheduledPosts
            .Where(p => p.ScheduleRunItemId != null
                && runItemIds.Contains(p.ScheduleRunItemId.Value)
                && p.Status == ScheduledPostStatus.Pending)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduledPost>> ListAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? aiUserId,
        ScheduledPostStatus? status, CancellationToken ct)
    {
        var query = dbContext.ScheduledPosts
            .Where(p => p.ScheduledAtUtc >= fromUtc && p.ScheduledAtUtc < toUtc);
        if (aiUserId is not null)
        {
            query = query.Where(p => p.AiUserId == aiUserId);
        }
        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        return await query.OrderBy(p => p.ScheduledAtUtc).ToListAsync(ct);
    }

    public async Task<DateTimeOffset?> GetNextPendingDueUtcAsync(CancellationToken ct)
    {
        return await dbContext.ScheduledPosts
            .Where(p => p.Status == ScheduledPostStatus.Pending)
            .MinAsync(p => (DateTimeOffset?)p.ScheduledAtUtc, ct);
    }

    public async Task<IReadOnlyList<ScheduledPost>> GetByRunItemIdsAsync(
        IReadOnlyList<Guid> runItemIds, CancellationToken ct)
    {
        return await dbContext.ScheduledPosts
            .Where(p => p.ScheduleRunItemId != null && runItemIds.Contains(p.ScheduleRunItemId.Value))
            .ToListAsync(ct);
    }
}

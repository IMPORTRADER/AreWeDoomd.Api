using AreWeDoomd.Domain.Scheduling;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IScheduledPostRepository
{
    Task AddAsync(ScheduledPost post, CancellationToken ct);
    Task<ScheduledPost?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ScheduledPost>> GetDuePendingAsync(DateTimeOffset nowUtc, CancellationToken ct);
    Task<bool> TryClaimAsync(Guid id, Guid claimToken, Guid publishedPostId, CancellationToken ct);
    Task<bool> WasClaimWonAsync(Guid id, Guid claimToken, CancellationToken ct);
    Task<IReadOnlyList<ScheduledPost>> GetStuckPublishingAsync(DateTimeOffset claimedBeforeUtc, CancellationToken ct);
    Task<IReadOnlyList<ScheduledPost>> GetPendingByRunItemIdsAsync(IReadOnlyList<Guid> runItemIds, CancellationToken ct);
    Task<IReadOnlyList<ScheduledPost>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? aiUserId, ScheduledPostStatus? status, CancellationToken ct);
    Task<DateTimeOffset?> GetNextPendingDueUtcAsync(CancellationToken ct);
    Task<IReadOnlyList<ScheduledPost>> GetByRunItemIdsAsync(IReadOnlyList<Guid> runItemIds, CancellationToken ct);
}

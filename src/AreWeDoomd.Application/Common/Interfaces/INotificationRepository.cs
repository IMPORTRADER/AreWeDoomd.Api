using AreWeDoomd.Domain.Notifications;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface INotificationRepository
{
    /// <summary>
    /// Persists the notification. Returns false when a (UserId, DedupeKey) duplicate
    /// already exists (idempotent delivery — the row is silently skipped).
    /// Throws for any other persistence failure.
    /// </summary>
    Task<bool> AddAsync(Notification notification, CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> GetRecentAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken);
}

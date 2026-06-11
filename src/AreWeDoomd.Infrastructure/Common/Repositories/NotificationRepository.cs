using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Notifications;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class NotificationRepository(AreWeDoomdDbContext dbContext) : INotificationRepository
{
    private const int UniqueViolationErrorNumber = 2627;
    private const int UniqueIndexViolationErrorNumber = 2601;

    public async Task<bool> AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        dbContext.Notifications.Add(notification);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Already delivered to this recipient — detach and skip.
            dbContext.Entry(notification).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<IReadOnlyList<Notification>> GetRecentAsync(
        Guid userId, int limit, CancellationToken cancellationToken)
    {
        return await dbContext.Notifications
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .CountAsync(cancellationToken);
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        return dbContext.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsRead, true)
                    .SetProperty(x => x.ReadAt, readAt),
                cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && (sqlException.Number == UniqueViolationErrorNumber
                || sqlException.Number == UniqueIndexViolationErrorNumber);
    }
}

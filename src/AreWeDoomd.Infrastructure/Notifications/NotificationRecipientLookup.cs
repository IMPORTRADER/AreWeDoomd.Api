using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Notifications;

public sealed class NotificationRecipientLookup(AreWeDoomdDbContext dbContext) : INotificationRecipientLookup
{
    public Task<Guid?> GetPostOwnerIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return dbContext.Posts
            .Where(post => post.Id == postId)
            .Select(post => (Guid?)post.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Notifications;

public sealed class NotificationRecipientLookup(AreWeDoomdDbContext dbContext) : INotificationRecipientLookup
{
    public Task<NotificationRecipientIdentity?> GetPostOwnerAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Posts
            .Where(post => post.Id == postId)
            .Join(
                dbContext.Users,
                post => post.UserId,
                user => user.Id,
                (_, user) => new NotificationRecipientIdentity(
                    user.Id,
                    user.UserType == UserType.Ai
                        ? NotificationRecipientType.Ai
                        : NotificationRecipientType.Human))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

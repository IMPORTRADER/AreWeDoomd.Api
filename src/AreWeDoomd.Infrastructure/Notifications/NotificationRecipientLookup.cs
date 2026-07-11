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
                    user.UserType == UserType.Ai && user.DeactivatedAt == null
                        ? NotificationRecipientType.Ai
                        : NotificationRecipientType.Human))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRecipientIdentity>> GetCommenterIdentitiesAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Comments
            .Where(comment => comment.PostId == postId)
            .Join(
                dbContext.Users,
                comment => comment.UserId,
                user => user.Id,
                (_, user) => new
                {
                    user.Id,
                    user.UserType,
                    user.DeactivatedAt
                })
            .Distinct()
            .Select(user => new NotificationRecipientIdentity(
                user.Id,
                user.UserType == UserType.Ai && user.DeactivatedAt == null
                    ? NotificationRecipientType.Ai
                    : NotificationRecipientType.Human))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRecipientIdentity>> GetIdentitiesByUsernamesAsync(
        IReadOnlyCollection<string> usernames,
        CancellationToken cancellationToken = default)
    {
        if (usernames.Count == 0)
        {
            return [];
        }

        return await dbContext.Users
            .Where(user => usernames.Contains(user.Username))
            .Select(user => new NotificationRecipientIdentity(
                user.Id,
                user.UserType == UserType.Ai && user.DeactivatedAt == null
                    ? NotificationRecipientType.Ai
                    : NotificationRecipientType.Human))
            .ToListAsync(cancellationToken);
    }
}

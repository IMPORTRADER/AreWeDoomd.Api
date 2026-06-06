namespace AreWeDoomd.Application.Notifications.Engine;

public interface INotificationRecipientLookup
{
    Task<NotificationRecipientIdentity?> GetPostOwnerAsync(
        Guid postId,
        CancellationToken cancellationToken = default);
}

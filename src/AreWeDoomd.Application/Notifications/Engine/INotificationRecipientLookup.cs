namespace AreWeDoomd.Application.Notifications.Engine;

public interface INotificationRecipientLookup
{
    Task<Guid?> GetPostOwnerIdAsync(Guid postId, CancellationToken cancellationToken = default);
}

using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Dispatching;

public interface INotificationDeliveryService
{
    Task DeliverAsync(
        ActivityNotification notification,
        CancellationToken cancellationToken = default);
}

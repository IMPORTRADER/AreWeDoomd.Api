using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Dispatching;

public interface INotificationDispatcher
{
    Task DispatchAsync(
        ActivityNotification notification,
        CancellationToken cancellationToken = default);
}

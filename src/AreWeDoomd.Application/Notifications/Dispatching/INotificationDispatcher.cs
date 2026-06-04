using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Dispatching;

public interface INotificationDispatcher
{
    Task DispatchAsync(
        EventNotification notification,
        CancellationToken cancellationToken = default);
}

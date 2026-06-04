using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public interface INotificationEngine
{
    Task<EventNotification> ComputeAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default);
}

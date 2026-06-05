using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public interface INotificationEngine
{
    Task<ActivityNotification> ComputeAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default);
}

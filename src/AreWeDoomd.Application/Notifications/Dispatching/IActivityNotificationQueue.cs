using AreWeDoomd.Application.Notifications.Engine;

namespace AreWeDoomd.Application.Notifications.Dispatching;

public interface IActivityNotificationQueue
{
    ValueTask EnqueueAsync(ActivityContext context, CancellationToken cancellationToken = default);

    ValueTask<ActivityContext> DequeueAsync(CancellationToken cancellationToken = default);
}

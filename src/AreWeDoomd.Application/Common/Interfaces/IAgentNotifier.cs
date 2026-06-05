using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAgentNotifier
{
    Task NotifyAsync(ActivityNotification notification, CancellationToken cancellationToken = default);
}

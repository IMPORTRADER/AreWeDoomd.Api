using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAgentNotifier
{
    Task NotifyAsync(EventNotification notification, CancellationToken cancellationToken = default);
}

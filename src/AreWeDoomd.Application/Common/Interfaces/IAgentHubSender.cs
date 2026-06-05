using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAgentHubSender
{
    Task SendAsync(ActivityNotification notification, CancellationToken cancellationToken = default);
}

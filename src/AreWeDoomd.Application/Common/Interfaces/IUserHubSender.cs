using AreWeDoomd.Application.Notifications.Dispatching;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IUserHubSender
{
    Task SendAsync(Guid userId, UserNotificationDto notification, CancellationToken cancellationToken = default);
}

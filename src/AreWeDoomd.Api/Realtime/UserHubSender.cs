using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Dispatching;
using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

public sealed class UserHubSender(
    IHubContext<UserNotificationHub, IUserNotificationClient> hubContext) : IUserHubSender
{
    public Task SendAsync(Guid userId, UserNotificationDto notification, CancellationToken cancellationToken = default)
    {
        return hubContext.Clients
            .User(userId.ToString())
            .ReceiveNotification(notification);
    }
}

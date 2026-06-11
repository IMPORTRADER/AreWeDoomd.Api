using AreWeDoomd.Application.Notifications.Dispatching;

namespace AreWeDoomd.Api.Realtime;

public interface IUserNotificationClient
{
    Task ReceiveNotification(UserNotificationDto notification);
}

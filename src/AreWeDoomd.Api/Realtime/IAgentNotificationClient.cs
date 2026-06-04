using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Api.Realtime;

public interface IAgentNotificationClient
{
    Task ReceiveEvent(EventNotification notification);
}

using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Api.Realtime;

public interface IAgentNotificationClient
{
    Task ReceiveEvent(ActivityNotification notification);
    Task ReceiveScheduleRun(ScheduleRunRequest request);
}

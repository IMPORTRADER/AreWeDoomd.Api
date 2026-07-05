using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IScheduleRunHubSender
{
    Task SendAsync(ScheduleRunRequest request, CancellationToken cancellationToken = default);
}

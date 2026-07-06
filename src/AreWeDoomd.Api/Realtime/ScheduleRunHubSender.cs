using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

public sealed class ScheduleRunHubSender : IScheduleRunHubSender
{
    private readonly IHubContext<AgentNotificationHub, IAgentNotificationClient> _hubContext;

    public ScheduleRunHubSender(IHubContext<AgentNotificationHub, IAgentNotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendAsync(ScheduleRunRequest request, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.ReceiveScheduleRun(request);
    }
}

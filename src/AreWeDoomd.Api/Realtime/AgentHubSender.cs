using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

public sealed class AgentHubSender : IAgentHubSender
{
    private readonly IHubContext<AgentNotificationHub, IAgentNotificationClient> _hubContext;

    public AgentHubSender(IHubContext<AgentNotificationHub, IAgentNotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendAsync(ActivityNotification notification, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.ReceiveEvent(notification);
    }
}

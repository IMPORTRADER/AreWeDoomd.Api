using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

public sealed class SignalRAgentNotifier : IAgentNotifier
{
    private readonly IHubContext<AgentNotificationHub, IAgentNotificationClient> _hubContext;

    public SignalRAgentNotifier(IHubContext<AgentNotificationHub, IAgentNotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAsync(ActivityNotification notification, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.ReceiveEvent(notification);
    }
}

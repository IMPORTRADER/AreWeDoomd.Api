using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

public sealed class AgentNotificationHub : Hub<IAgentNotificationClient>
{
}

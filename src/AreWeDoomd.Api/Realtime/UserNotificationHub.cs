using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

[Authorize]
public sealed class UserNotificationHub : Hub<IUserNotificationClient>
{
}

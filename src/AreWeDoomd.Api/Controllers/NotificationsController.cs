using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Notifications;
using AreWeDoomd.Application.Features.Notifications.Commands.MarkAllRead;
using AreWeDoomd.Application.Features.Notifications.Queries.GetUnreadCount;
using AreWeDoomd.Application.Features.Notifications.Queries.GetUserNotifications;
using AreWeDoomd.Application.Notifications.Dispatching;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UserNotificationDto>>> GetNotifications(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new GetUserNotificationsQuery(userId), cancellationToken);

        return this.ToActionResult(result, notifications => notifications);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new GetUnreadNotificationCountQuery(userId), cancellationToken);

        return this.ToActionResult(result, count => new UnreadCountResponse(count));
    }

    [HttpPost("mark-all-read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new MarkAllNotificationsReadCommand(userId), cancellationToken);

        return this.ToNoContentResult(result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }
}

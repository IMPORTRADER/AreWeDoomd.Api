using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Notifications.Dispatching;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Security.Claims;

namespace AreWeDoomd.Api.Filters;

public sealed class ActivityPublishingFilter(
    PublishActivityAttribute attribute,
    IActivityNotificationQueue notificationQueue,
    ILogger<ActivityPublishingFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Exception is not null)
        {
            return;
        }

        if (executed.Result is IStatusCodeActionResult { StatusCode: int code } && (code < 200 || code >= 300))
        {
            return;
        }

        var user = executed.HttpContext.User;

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (role != nameof(UserType.Ai) && role != nameof(UserType.Human))
        {
            logger.LogError("PublishActivity: invalid or missing role claim '{Role}', activity skipped.", role);
            return;
        }

        var actorType = role == nameof(UserType.Ai) ? ActorType.Ai : ActorType.Human;

        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var actorDisplayName = user.FindFirstValue(ClaimTypes.Name) ?? actorId;

        var objectId = "";
        ActivityObjectType objectType = default;
        string? objectTextPreview = null;
        if (executed.Result is ObjectResult { Value: IActivityObjectCarrier carrier })
        {
            objectId = carrier.ActivityObjectId;
            objectType = carrier.ActivityObjectType;
            objectTextPreview = carrier.ActivityObjectTextPreview;
        }

        var routeValues = executed.HttpContext.Request.RouteValues;
        var targetId = attribute.TargetIdParam is not null
            ? routeValues[attribute.TargetIdParam]?.ToString() ?? ""
            : "";

        var activityContext = new ActivityContext(
            ActivityType: attribute.ActivityType,
            ActorId: actorId,
            ActorType: actorType,
            ActorDisplayName: actorDisplayName,
            ObjectId: objectId,
            ObjectType: objectType,
            ObjectTextPreview: objectTextPreview,
            TargetId: targetId,
            TargetType: attribute.TargetType,
            OccurredAt: DateTimeOffset.UtcNow);

        try
        {
            await notificationQueue.EnqueueAsync(activityContext, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "ActivityPublishingFilter: failed to enqueue activity {ActivityType}.",
                activityContext.ActivityType);
        }
    }
}

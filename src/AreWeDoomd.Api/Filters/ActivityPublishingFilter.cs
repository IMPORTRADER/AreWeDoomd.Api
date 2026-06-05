using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Security.Claims;

namespace AreWeDoomd.Api.Filters;

public sealed class ActivityPublishingFilter(
    INotificationEngine notificationEngine,
    IAgentHubSender agentHubSender,
    ILogger<ActivityPublishingFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        var attribute = executed.ActionDescriptor
            .EndpointMetadata
            .OfType<PublishActivityAttribute>()
            .FirstOrDefault();

        if (attribute is null) return;
        if (executed.Exception is not null) return;
        if (executed.Result is IStatusCodeActionResult { StatusCode: int code } && (code < 200 || code >= 300)) return;

        var user = executed.HttpContext.User;

        // Actor tipi — JWT Role claim'inden; güvenilmezse logla ve aktiviteyi atla.
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (role != nameof(UserType.Ai) && role != nameof(UserType.Human))
        {
            logger.LogError("PublishActivity: geçersiz/eksik role claim '{Role}', aktivite atlandı.", role);
            return;
        }
        var actorType = role == nameof(UserType.Ai) ? ActorType.Agent : ActorType.Human;

        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var actorDisplayName = user.FindFirstValue(ClaimTypes.Name) ?? actorId;

        // Object — response body'deki carrier'dan (id + tip + önizleme).
        var objectId = "";
        ActivityObjectType objectType = default;
        string? objectTextPreview = null;
        if (executed.Result is ObjectResult { Value: IActivityObjectCarrier carrier })
        {
            objectId = carrier.ActivityObjectId;
            objectType = carrier.ActivityObjectType;
            objectTextPreview = carrier.ActivityObjectTextPreview;
        }

        // Target — route value'dan.
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

        _ = PublishAsync(activityContext); // '_' (underscore) kullanımı -> fire-and-forget: response'u bekletmez
    }

    private async Task PublishAsync(ActivityContext activityContext)
    {
        try
        {
            var notification = await notificationEngine.ComputeAsync(activityContext, CancellationToken.None);
            await agentHubSender.SendAsync(notification, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ActivityPublishingFilter: failed to dispatch activity {ActivityType}.",
                activityContext.ActivityType);
        }
    }
}

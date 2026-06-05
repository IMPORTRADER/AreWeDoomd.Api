using System.Security.Claims;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Engine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AreWeDoomd.Api.Filters;

public sealed class ActivityEmissionFilter(
    INotificationEngine engine,
    IAgentNotifier notifier) : IAsyncActionFilter
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

        var actorId = executed.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var actorDisplayName = executed.HttpContext.User.FindFirstValue(ClaimTypes.Name) ?? actorId;

        var routeValues = executed.HttpContext.Request.RouteValues;
        var objectId = attribute.ObjectIdParam is not null
            ? routeValues[attribute.ObjectIdParam]?.ToString() ?? ""
            : "";
        var targetId = attribute.TargetIdParam is not null
            ? routeValues[attribute.TargetIdParam]?.ToString() ?? ""
            : "";

        var activityContext = new ActivityContext(
            ActivityType: attribute.ActivityType,
            ActorId: actorId,
            ActorType: attribute.ActorType,
            ActorDisplayName: actorDisplayName,
            ObjectId: objectId,
            ObjectType: attribute.ObjectType,
            ObjectTextPreview: null,
            TargetId: targetId,
            TargetType: attribute.TargetType,
            OccurredAt: DateTimeOffset.UtcNow);

        _ = Task.Run(async () =>
        {
            var notification = await engine.ComputeAsync(activityContext, CancellationToken.None);
            await notifier.NotifyAsync(notification, CancellationToken.None);
        });
    }
}

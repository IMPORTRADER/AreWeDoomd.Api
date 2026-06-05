using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Notifications.Engine;

namespace AreWeDoomd.Infrastructure.Notifications;

public sealed class PassthroughNotificationEngine : INotificationEngine
{
    public Task<ActivityNotification> ComputeAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var notification = new ActivityNotification(
            ActivityId: $"act_{Guid.NewGuid():N}",
            ActivityType: context.ActivityType,
            OccurredAt: context.OccurredAt,
            Actor: new ActivityActor(context.ActorId, context.ActorType, context.ActorDisplayName),
            Object: new ActivityObject(context.ObjectId, context.ObjectType, context.ObjectTextPreview),
            Target: new ActivityTarget(context.TargetId, context.TargetType),
            Recipients: []);

        return Task.FromResult(notification);
    }
}

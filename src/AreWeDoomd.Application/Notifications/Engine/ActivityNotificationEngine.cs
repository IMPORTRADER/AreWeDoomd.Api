using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public sealed class ActivityNotificationEngine(IEnumerable<IActivityNotificationRule> rules) : INotificationEngine
{
    public async Task<ActivityNotification> ComputeAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var recipients = new List<NotificationRecipient>();

        foreach (var rule in rules.Where(rule => rule.CanHandle(context)))
        {
            var resolvedRecipients = await rule.ResolveRecipientsAsync(context, cancellationToken);
            recipients.AddRange(resolvedRecipients);
        }

        recipients = recipients
            .Where(recipient => !string.Equals(
                recipient.UserId,
                context.ActorId,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(recipient => recipient.DedupeKey)
            .Select(group => group.First())
            .ToList();

        return new ActivityNotification(
            ActivityId: $"act_{Guid.NewGuid():N}",
            ActivityType: context.ActivityType,
            OccurredAt: context.OccurredAt,
            Actor: new ActivityActor(context.ActorId, context.ActorType, context.ActorDisplayName),
            Object: new ActivityObject(context.ObjectId, context.ObjectType, context.ObjectTextPreview),
            Target: new ActivityTarget(context.TargetId, context.TargetType),
            Recipients: recipients);
    }
}

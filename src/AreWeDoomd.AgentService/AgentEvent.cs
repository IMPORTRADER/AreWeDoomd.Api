using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.AgentService;

/// <summary>
/// The AgentService's internal representation of an activity that the agent
/// should react to. Mapped from <see cref="ActivityNotification"/> on arrival
/// so agent logic stays decoupled from the wire contract.
/// </summary>
public sealed record AgentEvent(
    string ActivityId,
    ActivityType ActivityType,
    DateTimeOffset OccurredAt,
    AgentEvent.ActorInfo Actor,
    AgentEvent.ContentInfo Content,
    AgentEvent.TargetInfo Target,
    IReadOnlyList<AgentEvent.RecipientInfo> Recipients)
{
    /// <summary>Who performed the activity.</summary>
    public sealed record ActorInfo(
        string Id,
        ActorType Type,
        string DisplayName);

    /// <summary>The object that was acted upon (e.g. the comment that was created).</summary>
    public sealed record ContentInfo(
        string Id,
        ActivityObjectType Type,
        string? TextPreview);

    /// <summary>The context in which the activity occurred (e.g. the post being commented on).</summary>
    public sealed record TargetInfo(
        string Id,
        ActivityTargetType Type);

    /// <summary>One party that should be notified about this activity.</summary>
    public sealed record RecipientInfo(
        string UserId,
        NotificationRecipientType RecipientType,
        NotificationReason Reason,
        string Template,
        IReadOnlyDictionary<string, string> Params,
        string DedupeKey,
        NotificationPriority Priority);

    /// <summary>
    /// Maps an <see cref="ActivityNotification"/> received from the hub into
    /// an <see cref="AgentEvent"/> for use within the agent pipeline.
    /// </summary>
    public static AgentEvent From(ActivityNotification notification) =>
        new(
            notification.ActivityId,
            notification.ActivityType,
            notification.OccurredAt,
            new ActorInfo(
                notification.Actor.Id,
                notification.Actor.Type,
                notification.Actor.DisplayName),
            new ContentInfo(
                notification.Object.Id,
                notification.Object.Type,
                notification.Object.TextPreview),
            new TargetInfo(
                notification.Target.Id,
                notification.Target.Type),
            notification.Recipients
                .Select(r => new RecipientInfo(
                    r.UserId,
                    r.RecipientType,
                    r.Reason,
                    r.Template,
                    r.Params,
                    r.DedupeKey,
                    r.Priority))
                .ToList());
}

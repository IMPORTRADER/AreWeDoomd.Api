namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record EventNotification(
    string ActivityId,
    string ActivityType,
    DateTimeOffset OccurredAt,
    ActivityActor Actor,
    ActivityObject Object,
    ActivityTarget Target,
    List<NotificationRecipient> Recipients);

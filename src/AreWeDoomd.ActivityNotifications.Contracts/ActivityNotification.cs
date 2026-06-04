namespace AreWeDoomd.ActivityNotifications.Contracts;

public sealed record ActivityNotification(
    string ActivityId,
    ActivityType ActivityType,
    DateTimeOffset OccurredAt,
    ActivityActor Actor,
    ActivityObject Object,
    ActivityTarget Target,
    List<NotificationRecipient> Recipients);

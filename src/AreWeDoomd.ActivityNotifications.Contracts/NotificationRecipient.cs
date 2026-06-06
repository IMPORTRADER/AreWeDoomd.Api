namespace AreWeDoomd.ActivityNotifications.Contracts;

public sealed record NotificationRecipient(
    string UserId,
    NotificationRecipientType RecipientType,
    NotificationReason Reason,
    string Template,
    Dictionary<string, string> Params,
    string DedupeKey,
    NotificationPriority Priority);

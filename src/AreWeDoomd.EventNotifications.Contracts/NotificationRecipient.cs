namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record NotificationRecipient(
    string UserId,
    string Reason,
    string Template,
    Dictionary<string, string> Params,
    string DedupeKey,
    NotificationPriority Priority);

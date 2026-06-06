using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public sealed record NotificationRecipientIdentity(
    Guid UserId,
    NotificationRecipientType RecipientType);

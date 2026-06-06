using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public interface IActivityNotificationRule
{
    bool CanHandle(ActivityContext context);

    Task<IReadOnlyList<NotificationRecipient>> ResolveRecipientsAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default);
}

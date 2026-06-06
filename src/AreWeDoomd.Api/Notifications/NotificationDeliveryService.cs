using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Dispatching;

namespace AreWeDoomd.Api.Notifications;

public sealed class NotificationDeliveryService(
    IAgentHubSender agentHubSender,
    ILogger<NotificationDeliveryService> logger) : INotificationDeliveryService
{
    public async Task DeliverAsync(ActivityNotification notification, CancellationToken cancellationToken = default)
    {
        var aiRecipients = notification.Recipients
            .Where(recipient => recipient.RecipientType == NotificationRecipientType.Ai)
            .ToList();

        var hasAiRecipients = aiRecipients.Count > 0;

        if (hasAiRecipients)
        {
            await SendToAiRecipientsAsync(notification, aiRecipients, cancellationToken);
            return;
        }

        var hasOnlyHumanRecipients = notification.Recipients.Count > 0
            && notification.Recipients.All(recipient => recipient.RecipientType == NotificationRecipientType.Human);

        if (hasOnlyHumanRecipients)
        {
            await SendToHumanRecipientsAsync(notification, cancellationToken);
        }
    }

    private Task SendToAiRecipientsAsync(ActivityNotification notification, List<NotificationRecipient> aiRecipients, CancellationToken cancellationToken)
    {
        var agentNotification = notification with { Recipients = aiRecipients };
        return agentHubSender.SendAsync(agentNotification, cancellationToken);
    }

    private Task SendToHumanRecipientsAsync(ActivityNotification notification, CancellationToken cancellationToken)
    {
        // Not implemented yet: recipient listesi tamamen Human ise, kullanıcıya bildirim gönderilebilir.
        logger.LogDebug(
            "Human notification SignalR hub is not implemented yet. Activity {ActivityId} was not delivered.",
            notification.ActivityId);

        return Task.CompletedTask;
    }
}

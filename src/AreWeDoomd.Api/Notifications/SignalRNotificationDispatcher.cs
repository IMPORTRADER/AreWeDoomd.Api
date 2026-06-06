using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Dispatching;

namespace AreWeDoomd.Api.Notifications;

public sealed class SignalRNotificationDispatcher(
    IAgentHubSender agentHubSender,
    ILogger<SignalRNotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(
        ActivityNotification notification,
        CancellationToken cancellationToken = default)
    {
        var aiRecipients = notification.Recipients
            .Where(recipient => recipient.RecipientType == NotificationRecipientType.Ai)
            .ToList();

        if (aiRecipients.Count > 0)
        {
            var agentNotification = notification with { Recipients = aiRecipients };
            await agentHubSender.SendAsync(agentNotification, cancellationToken);
            return;
        }

        if (notification.Recipients.Count > 0
            && notification.Recipients.All(recipient => recipient.RecipientType == NotificationRecipientType.Human))
        {
            await SendToHumanRecipientsAsync(notification, cancellationToken);
        }
    }

    private Task SendToHumanRecipientsAsync(
        ActivityNotification notification,
        CancellationToken cancellationToken)
    {
        // Not implemented yet: recipient listesi tamamen Human ise, kullanıcıya bildirim gönderilebilir.
        logger.LogDebug(
            "Human notification SignalR hub is not implemented yet. Activity {ActivityId} was not delivered.",
            notification.ActivityId);

        return Task.CompletedTask;
    }
}

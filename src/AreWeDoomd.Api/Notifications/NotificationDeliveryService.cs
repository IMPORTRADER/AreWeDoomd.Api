using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Dispatching;
using AreWeDoomd.Domain.Notifications;

namespace AreWeDoomd.Api.Notifications;

public sealed class NotificationDeliveryService(
    IAgentHubSender agentHubSender,
    INotificationRepository notificationRepository,
    IUserHubSender userHubSender,
    ILogger<NotificationDeliveryService> logger) : INotificationDeliveryService
{
    public async Task DeliverAsync(ActivityNotification notification, CancellationToken cancellationToken = default)
    {
        var aiRecipients = notification.Recipients
            .Where(recipient => recipient.RecipientType == NotificationRecipientType.Ai)
            .ToList();

        var humanRecipients = notification.Recipients
            .Where(recipient => recipient.RecipientType == NotificationRecipientType.Human)
            .ToList();

        if (aiRecipients.Count > 0)
        {
            await SendToAiRecipientsAsync(notification, aiRecipients, cancellationToken);
        }

        if (humanRecipients.Count > 0)
        {
            await SendToHumanRecipientsAsync(notification, humanRecipients, cancellationToken);
        }
    }

    private Task SendToAiRecipientsAsync(
        ActivityNotification notification,
        List<NotificationRecipient> aiRecipients,
        CancellationToken cancellationToken)
    {
        var agentNotification = notification with { Recipients = aiRecipients };
        return agentHubSender.SendAsync(agentNotification, cancellationToken);
    }

    private async Task SendToHumanRecipientsAsync(
        ActivityNotification notification,
        List<NotificationRecipient> humanRecipients,
        CancellationToken cancellationToken)
    {
        foreach (var recipient in humanRecipients)
        {
            if (!Guid.TryParse(recipient.UserId, out var userId))
            {
                logger.LogWarning(
                    "Skipping human notification for activity {ActivityId}: recipient id {RecipientId} is not a Guid.",
                    notification.ActivityId,
                    recipient.UserId);
                continue;
            }

            try
            {
                var entity = Notification.Create(
                    userId: userId,
                    activityId: notification.ActivityId,
                    activityType: notification.ActivityType.ToString(),
                    actorName: notification.Actor.DisplayName,
                    actorType: notification.Actor.Type.ToString(),
                    template: recipient.Template,
                    paramsJson: JsonSerializer.Serialize(recipient.Params),
                    dedupeKey: recipient.DedupeKey,
                    createdAt: notification.OccurredAt);

                var persisted = await notificationRepository.AddAsync(entity, cancellationToken);

                if (!persisted)
                {
                    logger.LogDebug(
                        "Human notification {DedupeKey} for user {UserId} already delivered; skipping push.",
                        recipient.DedupeKey,
                        userId);
                    continue;
                }

                await PushAsync(userId, entity, recipient.Params, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to persist human notification for activity {ActivityId} and user {UserId}.",
                    notification.ActivityId,
                    userId);
            }
        }
    }

    private async Task PushAsync(
        Guid userId,
        Notification entity,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var dto = new UserNotificationDto(
            Id: entity.Id,
            Template: entity.Template,
            Params: parameters,
            ActorName: entity.ActorName,
            ActorType: entity.ActorType,
            CreatedAt: entity.CreatedAt,
            IsRead: entity.IsRead);

        try
        {
            await userHubSender.SendAsync(userId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to push persisted notification {NotificationId} to user {UserId}.",
                entity.Id,
                userId);
        }
    }
}

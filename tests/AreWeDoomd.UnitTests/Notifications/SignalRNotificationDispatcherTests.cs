using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Notifications;
using AreWeDoomd.Application.Common.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class SignalRNotificationDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WhenNotificationHasAiRecipient_ShouldSendToAgentHub()
    {
        var agentHubSender = new Mock<IAgentHubSender>();
        agentHubSender
            .Setup(sender => sender.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dispatcher = new SignalRNotificationDispatcher(
            agentHubSender.Object,
            NullLogger<SignalRNotificationDispatcher>.Instance);
        var notification = BuildNotification([
            BuildRecipient(NotificationRecipientType.Ai)
        ]);

        await dispatcher.DispatchAsync(notification);

        agentHubSender.Verify(
            sender => sender.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenNotificationHasOnlyHumanRecipients_ShouldNotSendToAgentHub()
    {
        var agentHubSender = new Mock<IAgentHubSender>();
        var dispatcher = new SignalRNotificationDispatcher(
            agentHubSender.Object,
            NullLogger<SignalRNotificationDispatcher>.Instance);
        var notification = BuildNotification([
            BuildRecipient(NotificationRecipientType.Human)
        ]);

        await dispatcher.DispatchAsync(notification);

        agentHubSender.Verify(
            sender => sender.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenNotificationHasMixedRecipients_ShouldSendOnlyAiRecipientsToAgentHub()
    {
        ActivityNotification? sentNotification = null;
        var agentHubSender = new Mock<IAgentHubSender>();
        agentHubSender
            .Setup(sender => sender.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
            .Callback<ActivityNotification, CancellationToken>((notification, _) => sentNotification = notification)
            .Returns(Task.CompletedTask);

        var dispatcher = new SignalRNotificationDispatcher(
            agentHubSender.Object,
            NullLogger<SignalRNotificationDispatcher>.Instance);
        var notification = BuildNotification([
            BuildRecipient(NotificationRecipientType.Human),
            BuildRecipient(NotificationRecipientType.Ai)
        ]);

        await dispatcher.DispatchAsync(notification);

        sentNotification.ShouldNotBeNull();
        sentNotification!.Recipients.Count.ShouldBe(1);
        sentNotification.Recipients[0].RecipientType.ShouldBe(NotificationRecipientType.Ai);
    }

    private static ActivityNotification BuildNotification(List<NotificationRecipient> recipients)
    {
        return new ActivityNotification(
            ActivityId: $"act_{Guid.NewGuid():N}",
            ActivityType: ActivityType.CommentCreated,
            OccurredAt: DateTimeOffset.UtcNow,
            Actor: new ActivityActor(Guid.NewGuid().ToString(), ActorType.Human, "Ali"),
            Object: new ActivityObject(Guid.NewGuid().ToString(), ActivityObjectType.Comment, "test"),
            Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post),
            Recipients: recipients);
    }

    private static NotificationRecipient BuildRecipient(NotificationRecipientType recipientType)
    {
        var userId = Guid.NewGuid().ToString();

        return new NotificationRecipient(
            UserId: userId,
            RecipientType: recipientType,
            Reason: NotificationReason.PostOwner,
            Template: "post.comment.created",
            Params: new Dictionary<string, string>(),
            DedupeKey: $"test:{userId}",
            Priority: NotificationPriority.Normal);
    }
}

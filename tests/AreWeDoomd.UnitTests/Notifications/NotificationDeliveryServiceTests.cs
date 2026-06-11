using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Notifications;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Dispatching;
using AreWeDoomd.Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class NotificationDeliveryServiceTests
{
    private readonly Mock<IAgentHubSender> _agentHubSender = new();
    private readonly Mock<INotificationRepository> _notificationRepository = new();
    private readonly Mock<IUserHubSender> _userHubSender = new();

    private NotificationDeliveryService CreateService()
    {
        return new NotificationDeliveryService(
            _agentHubSender.Object,
            _notificationRepository.Object,
            _userHubSender.Object,
            NullLogger<NotificationDeliveryService>.Instance);
    }

    public NotificationDeliveryServiceTests()
    {
        _notificationRepository
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task DeliverAsync_WhenNotificationHasAiRecipient_ShouldSendToAgentHub()
    {
        var notification = BuildNotification([BuildRecipient(NotificationRecipientType.Ai)]);

        await CreateService().DeliverAsync(notification);

        _agentHubSender.Verify(
            sender => sender.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeliverAsync_WhenNotificationHasOnlyHumanRecipients_ShouldNotSendToAgentHub()
    {
        var notification = BuildNotification([BuildRecipient(NotificationRecipientType.Human)]);

        await CreateService().DeliverAsync(notification);

        _agentHubSender.Verify(
            sender => sender.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeliverAsync_WhenNotificationHasOnlyHumanRecipients_ShouldPersistAndPush()
    {
        var notification = BuildNotification([BuildRecipient(NotificationRecipientType.Human)]);

        await CreateService().DeliverAsync(notification);

        _notificationRepository.Verify(
            r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _userHubSender.Verify(
            s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<UserNotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeliverAsync_WhenNotificationHasMixedRecipients_ShouldRunBothLegs()
    {
        ActivityNotification? agentNotification = null;
        _agentHubSender
            .Setup(sender => sender.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
            .Callback<ActivityNotification, CancellationToken>((n, _) => agentNotification = n)
            .Returns(Task.CompletedTask);

        var notification = BuildNotification([
            BuildRecipient(NotificationRecipientType.Human),
            BuildRecipient(NotificationRecipientType.Ai)
        ]);

        await CreateService().DeliverAsync(notification);

        agentNotification.ShouldNotBeNull();
        agentNotification!.Recipients.Count.ShouldBe(1);
        agentNotification.Recipients[0].RecipientType.ShouldBe(NotificationRecipientType.Ai);

        _userHubSender.Verify(
            s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<UserNotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeliverAsync_WhenDuplicateNotification_ShouldNotPush()
    {
        _notificationRepository
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var notification = BuildNotification([BuildRecipient(NotificationRecipientType.Human)]);

        await CreateService().DeliverAsync(notification);

        _userHubSender.Verify(
            s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<UserNotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeliverAsync_WhenPersistFails_ShouldNotPushAndShouldContinueOtherRecipients()
    {
        var failingUserId = Guid.NewGuid();
        var okUserId = Guid.NewGuid();

        _notificationRepository
            .Setup(r => r.AddAsync(It.Is<Notification>(n => n.UserId == failingUserId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var notification = BuildNotification([
            BuildRecipient(NotificationRecipientType.Human, failingUserId),
            BuildRecipient(NotificationRecipientType.Human, okUserId)
        ]);

        await CreateService().DeliverAsync(notification);

        _userHubSender.Verify(s => s.SendAsync(failingUserId, It.IsAny<UserNotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _userHubSender.Verify(s => s.SendAsync(okUserId, It.IsAny<UserNotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeliverAsync_WhenNoRecipients_ShouldDoNothing()
    {
        var notification = BuildNotification([]);

        await CreateService().DeliverAsync(notification);

        _agentHubSender.Verify(s => s.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        _userHubSender.Verify(s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<UserNotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ActivityNotification BuildNotification(List<NotificationRecipient> recipients)
    {
        return new ActivityNotification(
            ActivityId: $"act_{Guid.NewGuid():N}",
            ActivityType: ActivityType.CommentCreated,
            OccurredAt: DateTimeOffset.UtcNow,
            Actor: new ActivityActor(Guid.NewGuid().ToString(), ActorType.Human, "MiraStone"),
            Object: new ActivityObject(Guid.NewGuid().ToString(), ActivityObjectType.Comment, "test"),
            Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post),
            Recipients: recipients);
    }

    private static NotificationRecipient BuildRecipient(NotificationRecipientType recipientType, Guid? userId = null)
    {
        var id = (userId ?? Guid.NewGuid()).ToString();

        return new NotificationRecipient(
            UserId: id,
            RecipientType: recipientType,
            Reason: NotificationReason.PostOwner,
            Template: "post.comment.created",
            Params: new Dictionary<string, string> { ["actor_name"] = "MiraStone" },
            DedupeKey: $"test:{id}",
            Priority: NotificationPriority.Normal);
    }
}

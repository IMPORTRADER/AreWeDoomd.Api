using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Notifications.Engine;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class ActivityNotificationEngineTests
{
    [Fact]
    public async Task ComputeAsync_WhenCommentCreatedForAnotherUsersPost_ShouldAddPostOwnerRecipient()
    {
        var postId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var postOwnerId = Guid.NewGuid();
        var lookup = new Mock<INotificationRecipientLookup>();
        lookup.Setup(l => l.GetPostOwnerAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRecipientIdentity(postOwnerId, NotificationRecipientType.Ai));

        var engine = BuildEngine(lookup.Object);
        var occurredAt = DateTimeOffset.UtcNow;
        var context = BuildCommentCreatedContext(postId, commentId, actorId: Guid.NewGuid(), occurredAt);

        var result = await engine.ComputeAsync(context);

        result.ActivityId.ShouldNotBeNullOrWhiteSpace();
        result.ActivityType.ShouldBe(ActivityType.CommentCreated);
        result.OccurredAt.ShouldBe(occurredAt);
        result.Actor.Id.ShouldBe(context.ActorId);
        result.Object.Id.ShouldBe(commentId.ToString());
        result.Target.Id.ShouldBe(postId.ToString());
        result.Recipients.Count.ShouldBe(1);

        var recipient = result.Recipients[0];
        recipient.UserId.ShouldBe(postOwnerId.ToString());
        recipient.RecipientType.ShouldBe(NotificationRecipientType.Ai);
        recipient.Reason.ShouldBe(NotificationReason.PostOwner);
        recipient.Template.ShouldBe("post.comment.created");
        recipient.Params["actor_name"].ShouldBe("Ali");
        recipient.Params["post_id"].ShouldBe(postId.ToString());
        recipient.Params["comment_id"].ShouldBe(commentId.ToString());
        recipient.Params["comment_preview"].ShouldBe("Harika!");
        recipient.DedupeKey.ShouldBe($"comment.created:{commentId}:post-owner:{postOwnerId}");
        recipient.Priority.ShouldBe(NotificationPriority.Normal);
    }

    [Fact]
    public async Task ComputeAsync_WhenActorOwnsPost_ShouldSkipSelfRecipient()
    {
        var postId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var lookup = new Mock<INotificationRecipientLookup>();
        lookup.Setup(l => l.GetPostOwnerAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRecipientIdentity(actorId, NotificationRecipientType.Human));

        var engine = BuildEngine(lookup.Object);
        var context = BuildCommentCreatedContext(postId, Guid.NewGuid(), actorId, DateTimeOffset.UtcNow);

        var result = await engine.ComputeAsync(context);

        result.Recipients.ShouldBeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_WhenNoRuleMatches_ShouldReturnNotificationWithoutRecipients()
    {
        var lookup = new Mock<INotificationRecipientLookup>();
        var engine = BuildEngine(lookup.Object);
        var context = new ActivityContext(
            ActivityType: ActivityType.PostLiked,
            ActorId: Guid.NewGuid().ToString(),
            ActorType: ActorType.Human,
            ActorDisplayName: "Ali",
            ObjectId: Guid.NewGuid().ToString(),
            ObjectType: ActivityObjectType.Post,
            ObjectTextPreview: null,
            TargetId: Guid.NewGuid().ToString(),
            TargetType: ActivityTargetType.Post,
            OccurredAt: DateTimeOffset.UtcNow);

        var result = await engine.ComputeAsync(context);

        result.Recipients.ShouldBeEmpty();
        lookup.Verify(
            l => l.GetPostOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ActivityNotificationEngine BuildEngine(INotificationRecipientLookup lookup)
    {
        return new ActivityNotificationEngine(
        [
            new CommentCreatedNotificationRule(lookup)
        ]);
    }

    private static ActivityContext BuildCommentCreatedContext(
        Guid postId,
        Guid commentId,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        return new ActivityContext(
            ActivityType: ActivityType.CommentCreated,
            ActorId: actorId.ToString(),
            ActorType: ActorType.Human,
            ActorDisplayName: "Ali",
            ObjectId: commentId.ToString(),
            ObjectType: ActivityObjectType.Comment,
            ObjectTextPreview: "Harika!",
            TargetId: postId.ToString(),
            TargetType: ActivityTargetType.Post,
            OccurredAt: occurredAt);
    }
}

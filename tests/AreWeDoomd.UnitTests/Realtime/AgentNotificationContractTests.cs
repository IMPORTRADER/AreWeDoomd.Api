using AreWeDoomd.ActivityNotifications.Contracts;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class AgentNotificationContractTests
{
    [Fact]
    public void ActivityNotification_Constructs_WithAllFields()
    {
        var actorId = Guid.NewGuid().ToString();
        var targetOwnerId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var notification = new ActivityNotification(
            ActivityId: "act_1",
            ActivityType: ActivityType.CommentCreated,
            OccurredAt: now,
            Actor: new ActivityActor(actorId, ActorType.Human, "Ali"),
            Object: new ActivityObject(Guid.NewGuid().ToString(), ActivityObjectType.Comment, "Harika!"),
            Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post, targetOwnerId),
            Recipients: [
                new NotificationRecipient(
                    UserId: targetOwnerId,
                    Reason: NotificationReason.PostOwner,
                    Template: "post.comment.created",
                    Params: new Dictionary<string, string> { ["actor_name"] = "Ali" },
                    DedupeKey: $"comment.created:comment_1:{targetOwnerId}",
                    Priority: NotificationPriority.Normal)
            ]);

        notification.ActivityId.ShouldBe("act_1");
        notification.ActivityType.ShouldBe(ActivityType.CommentCreated);
        notification.OccurredAt.ShouldBe(now);
        notification.Actor.Id.ShouldBe(actorId);
        notification.Actor.DisplayName.ShouldBe("Ali");
        notification.Object.TextPreview.ShouldBe("Harika!");
        notification.Target.OwnerId.ShouldBe(targetOwnerId);
        notification.Recipients.Count.ShouldBe(1);
        notification.Recipients[0].Priority.ShouldBe(NotificationPriority.Normal);
        notification.Recipients[0].Params["actor_name"].ShouldBe("Ali");
    }
}

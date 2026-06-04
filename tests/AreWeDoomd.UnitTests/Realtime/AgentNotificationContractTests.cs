using AreWeDoomd.EventNotifications.Contracts;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class AgentNotificationContractTests
{
    [Fact]
    public void EventNotification_Constructs_WithAllFields()
    {
        var actorId = Guid.NewGuid().ToString();
        var targetOwnerId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var notification = new EventNotification(
            ActivityId: "act_1",
            ActivityType: ActivityTypes.CommentCreated,
            OccurredAt: now,
            Actor: new ActivityActor(actorId, "user", "Ali"),
            Object: new ActivityObject(Guid.NewGuid().ToString(), "comment", "Harika!"),
            Target: new ActivityTarget(Guid.NewGuid().ToString(), "post", targetOwnerId),
            Recipients: [
                new NotificationRecipient(
                    UserId: targetOwnerId,
                    Reason: "post_owner",
                    Template: "post.comment.created",
                    Params: new Dictionary<string, string> { ["actor_name"] = "Ali" },
                    DedupeKey: $"comment.created:comment_1:{targetOwnerId}",
                    Priority: NotificationPriority.Normal)
            ]);

        notification.ActivityId.ShouldBe("act_1");
        notification.ActivityType.ShouldBe(ActivityTypes.CommentCreated);
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

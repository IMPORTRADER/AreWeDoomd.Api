using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Infrastructure.Notifications;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class PassthroughNotificationEngineTests
{
    [Fact]
    public async Task ComputeAsync_MapsContextToActivityNotification()
    {
        var engine = new PassthroughNotificationEngine();
        var occurredAt = DateTimeOffset.UtcNow;
        var context = new ActivityContext(
            ActivityType: ActivityType.CommentCreated,
            ActorId: "actor_1",
            ActorType: ActorType.Human,
            ActorDisplayName: "Ali",
            ObjectId: "obj_1",
            ObjectType: ActivityObjectType.Comment,
            ObjectTextPreview: "Harika!",
            TargetId: "target_1",
            TargetType: ActivityTargetType.Post,
            OccurredAt: occurredAt);

        var result = await engine.ComputeAsync(context);

        result.ActivityId.ShouldNotBeNullOrWhiteSpace();
        result.ActivityType.ShouldBe(ActivityType.CommentCreated);
        result.OccurredAt.ShouldBe(occurredAt);
        result.Actor.Id.ShouldBe("actor_1");
        result.Actor.Type.ShouldBe(ActorType.Human);
        result.Actor.DisplayName.ShouldBe("Ali");
        result.Object.Id.ShouldBe("obj_1");
        result.Object.Type.ShouldBe(ActivityObjectType.Comment);
        result.Object.TextPreview.ShouldBe("Harika!");
        result.Target.Id.ShouldBe("target_1");
        result.Target.Type.ShouldBe(ActivityTargetType.Post);
        result.Recipients.ShouldBeEmpty();
    }
}

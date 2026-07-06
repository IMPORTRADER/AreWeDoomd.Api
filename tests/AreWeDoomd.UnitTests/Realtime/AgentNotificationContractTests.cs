using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class AgentNotificationContractTests
{
    [Fact]
    public void ScheduleRunRequest_WhenLlmFieldsMissing_ShouldDeserializeWithSafeDefaults()
    {
        // Represents an "old-shape" hub message that predates the LLM fields.
        const string oldShapeJson = """
            {
                "runId": "11111111-1111-1111-1111-111111111111",
                "threshold": 5,
                "maxPostsPerAccount": 3,
                "postLengthGuide": 280,
                "strategy": 0,
                "windowStartUtc": "2026-01-01T00:00:00+00:00",
                "windowEndUtc": "2026-01-01T23:59:59+00:00",
                "items": []
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var request = JsonSerializer.Deserialize<ScheduleRunRequest>(oldShapeJson, options);

        request.ShouldNotBeNull();
        request.Model.ShouldBe("");
        request.ScoringModel.ShouldBe("");
        request.ThinkingEnabled.ShouldBe(false);
        request.ScoringTokensPerAccount.ShouldBe(512);
        request.CompositionTokensPerPost.ShouldBe(800);
    }


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
            Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post),
            Recipients: [
                new NotificationRecipient(
                    UserId: targetOwnerId,
                    RecipientType: NotificationRecipientType.Ai,
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
        notification.Recipients.Count.ShouldBe(1);
        notification.Recipients[0].RecipientType.ShouldBe(NotificationRecipientType.Ai);
        notification.Recipients[0].Priority.ShouldBe(NotificationPriority.Normal);
        notification.Recipients[0].Params["actor_name"].ShouldBe("Ali");
    }
}

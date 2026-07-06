using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Logging;
using AreWeDoomd.AgentService.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService;

public sealed class AgentNotificationListenerTests
{
    [Fact]
    public void HandleNotification_WhenQueueFull_ShouldLogDroppedEntry()
    {
        var queue = new AgentEventQueue();
        var decisionLog = new Mock<IDecisionLogWriter>();
        var listener = new AgentNotificationListener(
            Options.Create(new AgentServiceOptions()),
            queue,
            new ScheduleRunQueue(),
            decisionLog.Object,
            new FakeAgentOpsLogWriter(),
            NullLogger<AgentNotificationListener>.Instance);
        for (int i = 0; i < 100; i++) { queue.TryEnqueue(SampleAgentEvent($"fill-{i}")); }

        listener.HandleNotification(SampleNotification("overflow-1"));

        decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.Dropped && e.ActivityId == "overflow-1" &&
            e.AiUserId == "ai-user-id" && e.ActivityType == ActivityType.CommentCreated.ToString() &&
            e.Priority == NotificationPriority.Normal.ToString())), Times.Once);
    }

    [Fact]
    public void HandleNotification_WhenQueueAccepts_ShouldNotLog()
    {
        var queue = new AgentEventQueue();
        var decisionLog = new Mock<IDecisionLogWriter>();
        var listener = new AgentNotificationListener(
            Options.Create(new AgentServiceOptions()), queue, new ScheduleRunQueue(), decisionLog.Object,
            new FakeAgentOpsLogWriter(), NullLogger<AgentNotificationListener>.Instance);

        listener.HandleNotification(SampleNotification("ok-1"));

        decisionLog.Verify(w => w.TryLog(It.IsAny<DecisionLogEntry>()), Times.Never);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ActivityNotification SampleNotification(string activityId)
    {
        return new ActivityNotification(
            ActivityId: activityId,
            ActivityType: ActivityType.CommentCreated,
            OccurredAt: DateTimeOffset.UtcNow,
            Actor: new ActivityActor("actor-id", ActorType.Human, "Alice"),
            Object: new ActivityObject("comment-id", ActivityObjectType.Comment, "What do you think?"),
            Target: new ActivityTarget("post-id", ActivityTargetType.Post),
            Recipients: new List<NotificationRecipient>
            {
                new(
                    UserId: "ai-user-id",
                    RecipientType: NotificationRecipientType.Ai,
                    Reason: NotificationReason.PostOwner,
                    Template: "post.comment.created",
                    Params: new Dictionary<string, string>(),
                    DedupeKey: "dedupe",
                    Priority: NotificationPriority.Normal)
            });
    }

    private static AgentEvent SampleAgentEvent(string activityId)
    {
        return new AgentEvent(
            ActivityId: activityId,
            ActivityType: ActivityType.CommentCreated,
            OccurredAt: DateTimeOffset.UtcNow,
            Actor: new AgentEvent.ActorInfo("actor-id", ActorType.Human, "Alice"),
            Content: new AgentEvent.ContentInfo("comment-id", ActivityObjectType.Comment, "What do you think?"),
            Target: new AgentEvent.TargetInfo("post-id", ActivityTargetType.Post),
            Recipients: new List<AgentEvent.RecipientInfo>
            {
                new(
                    UserId: "ai-user-id",
                    RecipientType: NotificationRecipientType.Ai,
                    Reason: NotificationReason.PostOwner,
                    Template: "post.comment.created",
                    Params: new Dictionary<string, string>(),
                    DedupeKey: "dedupe",
                    Priority: NotificationPriority.Normal)
            });
    }
}

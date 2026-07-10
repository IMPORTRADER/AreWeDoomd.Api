using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Notifications.Engine;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class PostCreatedMentionNotificationRuleTests
{
    [Fact]
    public async Task ComputeAsync_WhenPostMentionsUsers_ShouldNotifyEachOnce()
    {
        var postId = Guid.NewGuid();
        var humanId = Guid.NewGuid();
        var aiId = Guid.NewGuid();

        var lookup = new Mock<INotificationRecipientLookup>();
        lookup.Setup(l => l.GetIdentitiesByUsernamesAsync(
                It.Is<IReadOnlyCollection<string>>(u => u.Contains("kaan") && u.Contains("robo")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationRecipientIdentity>
            {
                new(humanId, NotificationRecipientType.Human),
                new(aiId, NotificationRecipientType.Ai),
            });

        var engine = new ActivityNotificationEngine([new PostCreatedMentionNotificationRule(lookup.Object)]);
        var context = BuildPostCreatedContext(postId, preview: "hey @kaan and @robo, are we doomed?");

        var result = await engine.ComputeAsync(context);

        result.Recipients.Count.ShouldBe(2);

        var ai = result.Recipients.Single(r => r.UserId == aiId.ToString());
        ai.RecipientType.ShouldBe(NotificationRecipientType.Ai);
        ai.Reason.ShouldBe(NotificationReason.Mentioned);
        ai.Template.ShouldBe("user.mentioned");
        ai.Params["post_id"].ShouldBe(postId.ToString());
        ai.Params.ContainsKey("comment_id").ShouldBeFalse();
        ai.DedupeKey.ShouldBe($"post.created:{postId}:mentioned:{aiId}");
    }

    [Fact]
    public async Task ComputeAsync_WhenPostHasNoMentions_ShouldReturnNoRecipientsAndSkipLookup()
    {
        var lookup = new Mock<INotificationRecipientLookup>(MockBehavior.Strict);
        var engine = new ActivityNotificationEngine([new PostCreatedMentionNotificationRule(lookup.Object)]);
        var context = BuildPostCreatedContext(Guid.NewGuid(), preview: "no mentions here");

        var result = await engine.ComputeAsync(context);

        result.Recipients.ShouldBeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_WhenActivityIsNotPostCreated_ShouldNotHandle()
    {
        var lookup = new Mock<INotificationRecipientLookup>(MockBehavior.Strict);
        var rule = new PostCreatedMentionNotificationRule(lookup.Object);

        var context = BuildPostCreatedContext(Guid.NewGuid(), preview: "@someone") with
        {
            ActivityType = ActivityType.CommentCreated,
            ObjectType = ActivityObjectType.Comment,
        };

        rule.CanHandle(context).ShouldBeFalse();
    }

    private static ActivityContext BuildPostCreatedContext(Guid postId, string preview)
    {
        return new ActivityContext(
            ActivityType.PostCreated,
            ActorId: Guid.NewGuid().ToString(),
            ActorType.Human,
            ActorDisplayName: "Ali",
            ObjectId: postId.ToString(),
            ActivityObjectType.Post,
            ObjectTextPreview: preview,
            TargetId: "",
            ActivityTargetType.Post,
            OccurredAt: DateTimeOffset.UtcNow);
    }
}

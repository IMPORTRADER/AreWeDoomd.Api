using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Notifications.Engine;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class CommentLikedNotificationRuleTests
{
    [Fact]
    public async Task ResolveRecipientsAsync_WhenCommentHasHumanAuthor_ShouldCreateCommentAuthorRecipient()
    {
        var postId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var lookup = new Mock<INotificationRecipientLookup>(MockBehavior.Strict);
        lookup.Setup(l => l.GetCommentAuthorAsync(postId, commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRecipientIdentity(authorId, NotificationRecipientType.Human));
        var rule = new CommentLikedNotificationRule(lookup.Object);

        var recipients = await rule.ResolveRecipientsAsync(BuildContext(postId, commentId, Guid.NewGuid()));

        recipients.Count.ShouldBe(1);
        var recipient = recipients[0];
        recipient.UserId.ShouldBe(authorId.ToString());
        recipient.RecipientType.ShouldBe(NotificationRecipientType.Human);
        recipient.Reason.ShouldBe(NotificationReason.CommentAuthor);
        recipient.Template.ShouldBe("comment.liked");
        recipient.Params["actor_name"].ShouldBe("Ada");
        recipient.Params["post_id"].ShouldBe(postId.ToString());
        recipient.Params["comment_id"].ShouldBe(commentId.ToString());
        recipient.DedupeKey.ShouldBe($"comment.liked:{commentId}:author:{authorId}");
        recipient.Priority.ShouldBe(NotificationPriority.Normal);
    }

    [Fact]
    public async Task ComputeAsync_WhenActorLikesOwnComment_ShouldSuppressCommentAuthorRecipient()
    {
        var postId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var lookup = new Mock<INotificationRecipientLookup>(MockBehavior.Strict);
        lookup.Setup(l => l.GetCommentAuthorAsync(postId, commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRecipientIdentity(authorId, NotificationRecipientType.Human));
        var engine = new ActivityNotificationEngine([new CommentLikedNotificationRule(lookup.Object)]);

        var notification = await engine.ComputeAsync(BuildContext(postId, commentId, authorId));

        notification.Recipients.ShouldBeEmpty();
    }

    private static ActivityContext BuildContext(Guid postId, Guid commentId, Guid actorId)
    {
        return new ActivityContext(
            ActivityType.CommentLiked,
            actorId.ToString(),
            ActorType.Human,
            "Ada",
            commentId.ToString(),
            ActivityObjectType.Comment,
            null,
            postId.ToString(),
            ActivityTargetType.Post,
            DateTimeOffset.UtcNow);
    }
}

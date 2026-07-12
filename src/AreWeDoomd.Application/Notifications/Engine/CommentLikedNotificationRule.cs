using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public sealed class CommentLikedNotificationRule(INotificationRecipientLookup recipientLookup)
    : IActivityNotificationRule
{
    public bool CanHandle(ActivityContext context)
    {
        return context.ActivityType == ActivityType.CommentLiked
            && context.TargetType == ActivityTargetType.Post
            && context.ObjectType == ActivityObjectType.Comment;
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ResolveRecipientsAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(context.TargetId, out var postId)
            || !Guid.TryParse(context.ObjectId, out var commentId))
        {
            return [];
        }

        var author = await recipientLookup.GetCommentAuthorAsync(postId, commentId, cancellationToken);
        if (author is null)
        {
            return [];
        }

        var authorId = author.UserId.ToString();
        return
        [
            new NotificationRecipient(
                UserId: authorId,
                RecipientType: author.RecipientType,
                Reason: NotificationReason.CommentAuthor,
                Template: "comment.liked",
                Params: new Dictionary<string, string>
                {
                    ["actor_name"] = context.ActorDisplayName,
                    ["post_id"] = context.TargetId,
                    ["comment_id"] = context.ObjectId
                },
                DedupeKey: $"comment.liked:{context.ObjectId}:author:{authorId}",
                Priority: NotificationPriority.Normal)
        ];
    }
}

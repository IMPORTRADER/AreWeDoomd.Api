using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public sealed class CommentCreatedNotificationRule(INotificationRecipientLookup recipientLookup)
    : IActivityNotificationRule
{
    public bool CanHandle(ActivityContext context)
    {
        return context.ActivityType == ActivityType.CommentCreated
            && context.TargetType == ActivityTargetType.Post
            && context.ObjectType == ActivityObjectType.Comment;
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ResolveRecipientsAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(context.TargetId, out var postId))
        {
            return [];
        }

        // Keyed by user id so a user who both owns the post and commented on it is
        // notified once; the post owner reason wins. The engine drops the actor.
        var byUserId = new Dictionary<string, NotificationRecipient>(StringComparer.OrdinalIgnoreCase);

        var postOwner = await recipientLookup.GetPostOwnerAsync(postId, cancellationToken);
        if (postOwner is not null)
        {
            var ownerId = postOwner.UserId.ToString();
            byUserId[ownerId] = BuildRecipient(
                ownerId,
                postOwner.RecipientType,
                NotificationReason.PostOwner,
                template: "post.comment.created",
                dedupeKey: $"comment.created:{context.ObjectId}:post-owner:{ownerId}",
                context);
        }

        var commenters = await recipientLookup.GetCommenterIdentitiesAsync(postId, cancellationToken);
        foreach (var commenter in commenters ?? [])
        {
            var userId = commenter.UserId.ToString();
            if (byUserId.ContainsKey(userId))
            {
                continue;
            }

            byUserId[userId] = BuildRecipient(
                userId,
                commenter.RecipientType,
                NotificationReason.PostParticipant,
                template: "post.comment.reply",
                dedupeKey: $"comment.created:{context.ObjectId}:participant:{userId}",
                context);
        }

        return byUserId.Values.ToList();
    }

    private static NotificationRecipient BuildRecipient(
        string userId,
        NotificationRecipientType recipientType,
        NotificationReason reason,
        string template,
        string dedupeKey,
        ActivityContext context)
    {
        return new NotificationRecipient(
            UserId: userId,
            RecipientType: recipientType,
            Reason: reason,
            Template: template,
            Params: new Dictionary<string, string>
            {
                ["actor_name"] = context.ActorDisplayName,
                ["post_id"] = context.TargetId,
                ["comment_id"] = context.ObjectId,
                ["comment_preview"] = context.ObjectTextPreview ?? string.Empty
            },
            DedupeKey: dedupeKey,
            Priority: NotificationPriority.Normal);
    }
}

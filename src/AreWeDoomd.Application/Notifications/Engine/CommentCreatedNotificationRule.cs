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

        var postOwner = await recipientLookup.GetPostOwnerAsync(postId, cancellationToken);

        if (postOwner is null)
        {
            return [];
        }

        var recipientUserId = postOwner.UserId.ToString();

        return
        [
            new NotificationRecipient(
                UserId: recipientUserId,
                RecipientType: postOwner.RecipientType,
                Reason: NotificationReason.PostOwner,
                Template: "post.comment.created",
                Params: new Dictionary<string, string>
                {
                    ["actor_name"] = context.ActorDisplayName,
                    ["post_id"] = context.TargetId,
                    ["comment_id"] = context.ObjectId,
                    ["comment_preview"] = context.ObjectTextPreview ?? string.Empty
                },
                DedupeKey: $"comment.created:{context.ObjectId}:post-owner:{recipientUserId}",
                Priority: NotificationPriority.Normal)
        ];
    }
}

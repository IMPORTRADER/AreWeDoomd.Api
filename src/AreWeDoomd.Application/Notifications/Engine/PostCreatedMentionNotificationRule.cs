using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

/// <summary>
/// Post creation notifies ONLY mentioned users — a new post is not otherwise
/// a notification-worthy event. The post id travels as ObjectId because the
/// create endpoint has no route param to fill TargetId from.
/// </summary>
public sealed class PostCreatedMentionNotificationRule(INotificationRecipientLookup recipientLookup)
    : IActivityNotificationRule
{
    public bool CanHandle(ActivityContext context)
    {
        return context.ActivityType == ActivityType.PostCreated
            && context.ObjectType == ActivityObjectType.Post;
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ResolveRecipientsAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var mentionedUsernames = MentionParser.Extract(context.ObjectTextPreview);
        if (mentionedUsernames.Count == 0)
        {
            return [];
        }

        var identities = await recipientLookup.GetIdentitiesByUsernamesAsync(mentionedUsernames, cancellationToken);

        return (identities ?? [])
            .Select(identity =>
            {
                var userId = identity.UserId.ToString();
                return new NotificationRecipient(
                    UserId: userId,
                    RecipientType: identity.RecipientType,
                    Reason: NotificationReason.Mentioned,
                    Template: "user.mentioned",
                    Params: new Dictionary<string, string>
                    {
                        ["actor_name"] = context.ActorDisplayName,
                        ["post_id"] = context.ObjectId,
                        // Same key as the comment flow so the user.mentioned
                        // template reads one field for both sources.
                        ["comment_preview"] = context.ObjectTextPreview ?? string.Empty
                    },
                    DedupeKey: $"post.created:{context.ObjectId}:mentioned:{userId}",
                    Priority: NotificationPriority.Normal);
            })
            .ToList();
    }
}

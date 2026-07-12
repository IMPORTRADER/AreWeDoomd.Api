namespace AreWeDoomd.Application.Notifications.Engine;

public interface INotificationRecipientLookup
{
    Task<NotificationRecipientIdentity?> GetPostOwnerAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<NotificationRecipientIdentity?> GetCommentAuthorAsync(
        Guid postId,
        Guid commentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct identities of every user who has commented on the post (the post
    /// participants). Used to notify earlier commenters when a new reply lands.
    /// </summary>
    Task<IReadOnlyList<NotificationRecipientIdentity>> GetCommenterIdentitiesAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identities for the given usernames (match is case-insensitive under the
    /// database collation). Usernames that don't exist are silently omitted.
    /// </summary>
    Task<IReadOnlyList<NotificationRecipientIdentity>> GetIdentitiesByUsernamesAsync(
        IReadOnlyCollection<string> usernames,
        CancellationToken cancellationToken = default);
}

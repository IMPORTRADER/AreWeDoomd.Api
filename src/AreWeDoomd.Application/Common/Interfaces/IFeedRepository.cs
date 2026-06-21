using AreWeDoomd.Application.Features.Feed.Common;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IFeedRepository
{
    Task<IReadOnlyList<FeedPostResult>> GetFollowingFeedAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FeedPostResult>> GetGlobalCandidatesAsync(
        DateTimeOffset asOf,
        DateTimeOffset? createdAfter,
        int maxCandidates,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FeedPostResult>> AttachCommentsAsync(
        IReadOnlyList<FeedPostResult> posts,
        bool includeAllComments,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FeedPostResult>> GetUserPostsAsync(
        Guid userId,
        DateTimeOffset asOf,
        int offset,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FeedPostResult>> GetUserLikedPostsAsync(
        Guid userId,
        DateTimeOffset asOf,
        int offset,
        int limit,
        CancellationToken cancellationToken);
}

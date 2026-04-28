using AreWeDoomd.Application.Features.Feed.Common;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IFeedRepository
{
    Task<IReadOnlyList<FeedPostResult>> GetFollowingFeedAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FeedPostResult>> GetGlobalFeedAsync(bool includeAllComments, CancellationToken cancellationToken);
}

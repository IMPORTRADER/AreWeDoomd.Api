using AreWeDoomd.Application.Features.Posts.Common;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IFeedRepository
{
    Task<IReadOnlyList<PostResult>> GetFollowingFeedAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostResult>> GetGlobalFeedAsync(CancellationToken cancellationToken);
}

using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IUserFollowRepository
{
    Task<UserFollow?> GetAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FollowUserResult>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FollowUserResult>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(UserFollow userFollow, CancellationToken cancellationToken);
    Task DeleteAsync(UserFollow userFollow, CancellationToken cancellationToken);

    Task<int> CountFollowersAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSummaryResult>> GetFollowerSummariesAsync(
        Guid userId, Guid? requesterId, int offset, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSummaryResult>> GetFollowingSummariesAsync(
        Guid userId, Guid? requesterId, int offset, int limit, CancellationToken cancellationToken);
}

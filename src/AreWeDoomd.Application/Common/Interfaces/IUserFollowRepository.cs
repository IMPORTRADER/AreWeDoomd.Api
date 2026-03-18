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
}

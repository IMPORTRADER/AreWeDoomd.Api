using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class UserFollowRepository(AreWeDoomdDbContext dbContext) : IUserFollowRepository
{
    public Task<UserFollow?> GetAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken)
    {
        return dbContext.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == followingId, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken)
    {
        return dbContext.UserFollows
            .AnyAsync(f => f.FollowerId == followerId && f.FollowingId == followingId, cancellationToken);
    }

    public async Task<IReadOnlyList<FollowUserResult>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (
            from f in dbContext.UserFollows
            where f.FollowingId == userId
            join u in dbContext.Users on f.FollowerId equals u.Id
            orderby f.CreatedAt descending
            select new FollowUserResult(
                u.Id,
                u.Username,
                u.UserType.ToString(),
                u.Profile.ProfileImageUrl,
                f.CreatedAt)
            ).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FollowUserResult>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (
            from f in dbContext.UserFollows
            where f.FollowerId == userId
            join u in dbContext.Users on f.FollowingId equals u.Id
            orderby f.CreatedAt descending
            select new FollowUserResult(
                u.Id,
                u.Username,
                u.UserType.ToString(),
                u.Profile.ProfileImageUrl,
                f.CreatedAt)
            ).ToListAsync(cancellationToken);
    }

    public Task AddAsync(UserFollow userFollow, CancellationToken cancellationToken)
        => dbContext.UserFollows.AddAsync(userFollow, cancellationToken).AsTask();

    public Task DeleteAsync(UserFollow userFollow, CancellationToken cancellationToken)
    {
        dbContext.UserFollows.Remove(userFollow);
        return Task.CompletedTask;
    }

    public Task<int> CountFollowersAsync(Guid userId, CancellationToken cancellationToken)
        => dbContext.UserFollows.CountAsync(f => f.FollowingId == userId, cancellationToken);

    public async Task<IReadOnlyList<UserSummaryResult>> GetFollowerSummariesAsync(
        Guid userId, Guid? requesterId, int offset, int limit, CancellationToken cancellationToken)
    {
        var query =
            from f in dbContext.UserFollows
            where f.FollowingId == userId
            join u in dbContext.Users on f.FollowerId equals u.Id
            orderby f.CreatedAt descending
            select new UserSummaryResult(
                u.Id,
                u.Username,
                u.UserType.ToString(),
                u.Profile.ProfileImageUrl,
                u.Profile.Biography,
                requesterId != null && dbContext.UserFollows
                    .Any(x => x.FollowerId == requesterId && x.FollowingId == u.Id));

        return await query.Skip(offset).Take(limit).AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSummaryResult>> GetFollowingSummariesAsync(
        Guid userId, Guid? requesterId, int offset, int limit, CancellationToken cancellationToken)
    {
        var query =
            from f in dbContext.UserFollows
            where f.FollowerId == userId
            join u in dbContext.Users on f.FollowingId equals u.Id
            orderby f.CreatedAt descending
            select new UserSummaryResult(
                u.Id,
                u.Username,
                u.UserType.ToString(),
                u.Profile.ProfileImageUrl,
                u.Profile.Biography,
                requesterId != null && dbContext.UserFollows
                    .Any(x => x.FollowerId == requesterId && x.FollowingId == u.Id));

        return await query.Skip(offset).Take(limit).AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSummaryResult>> GetSuggestionsAsync(
        Guid? viewerId, int offset, int limit, CancellationToken cancellationToken)
    {
        var hasViewer = viewerId.HasValue;
        var viewer = viewerId ?? Guid.Empty;

        var query = dbContext.Users
            .Where(u => !hasViewer || u.Id != viewer)
            .Where(u => !hasViewer
                || !dbContext.UserFollows.Any(f => f.FollowerId == viewer && f.FollowingId == u.Id))
            // Random order (translated to SQL NEWID()) so suggestions surface a mix of
            // AI and Human accounts instead of always the same most-followed users.
            .OrderBy(u => Guid.NewGuid())
            .Select(u => new UserSummaryResult(
                u.Id,
                u.Username,
                u.UserType.ToString(),
                u.Profile.ProfileImageUrl,
                u.Profile.Biography,
                false));

        return await query.Skip(offset).Take(limit).AsNoTracking().ToListAsync(cancellationToken);
    }
}

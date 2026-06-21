using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class ProfileStatsRepository(AreWeDoomdDbContext dbContext) : IProfileStatsRepository
{
    public async Task<ProfileStatsResult> GetStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var postCount = await dbContext.Posts
            .CountAsync(p => p.UserId == userId, cancellationToken);

        var likeCount = await dbContext.Posts
            .Where(p => p.UserId == userId)
            .SumAsync(p => (int?)p.LikeCount, cancellationToken) ?? 0;

        var commentCount = await dbContext.Comments
            .CountAsync(c => c.UserId == userId, cancellationToken);

        var followerCount = await dbContext.UserFollows
            .CountAsync(f => f.FollowingId == userId, cancellationToken);

        var followingCount = await dbContext.UserFollows
            .CountAsync(f => f.FollowerId == userId, cancellationToken);

        return new ProfileStatsResult(postCount, likeCount, commentCount, followerCount, followingCount);
    }
}

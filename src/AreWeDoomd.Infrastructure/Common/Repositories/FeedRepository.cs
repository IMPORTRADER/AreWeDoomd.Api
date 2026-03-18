using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Posts.Common;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class FeedRepository(AreWeDoomdDbContext dbContext) : IFeedRepository
{
    public async Task<IReadOnlyList<PostResult>> GetFollowingFeedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var followingIds = await dbContext.UserFollows
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FollowingId)
            .ToListAsync(cancellationToken);

        return await dbContext.Posts
            .Where(p => followingIds.Contains(p.UserId))
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .Select(p => new PostResult(
                p.Id,
                p.UserId,
                p.Content,
                p.LikeCount,
                p.CommentCount,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PostResult>> GetGlobalFeedAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Posts
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .Select(p => new PostResult(
                p.Id,
                p.UserId,
                p.Content,
                p.LikeCount,
                p.CommentCount,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}

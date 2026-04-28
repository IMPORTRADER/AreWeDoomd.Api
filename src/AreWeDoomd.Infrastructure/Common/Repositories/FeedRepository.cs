using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Common;
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
            .Join(dbContext.Users,
                p => p.UserId,
                u => u.Id,
                (p, u) => new PostResult(
                    p.Id,
                    new PostAuthorResult(u.Id, u.Username, u.UserType.ToString(), u.Profile.ProfileImageUrl),
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
            .Join(dbContext.Users,
                p => p.UserId,
                u => u.Id,
                (p, u) => new PostResult(
                    p.Id,
                    new PostAuthorResult(u.Id, u.Username, u.UserType.ToString(), u.Profile.ProfileImageUrl),
                    p.Content,
                    p.LikeCount,
                    p.CommentCount,
                    p.CreatedAt,
                    p.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}

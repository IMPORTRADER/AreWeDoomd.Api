using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Common;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Feed.Common;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class FeedRepository(AreWeDoomdDbContext dbContext) : IFeedRepository
{
    private const int GuestCommentsPerPost = 2;
    private const int FeedCommentsPerPost = 6;

    public async Task<IReadOnlyList<FeedPostResult>> GetFollowingFeedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var followingIds = await dbContext.UserFollows
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FollowingId)
            .ToListAsync(cancellationToken);

        var posts = await dbContext.Posts
            .Where(p => followingIds.Contains(p.UserId))
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .Join(dbContext.Users,
                p => p.UserId,
                u => u.Id,
                (p, u) => new FeedPostResult(
                    p.Id,
                    new PostAuthorResult(u.Id, u.Username, u.UserType.ToString(), u.Profile.ProfileImageUrl),
                    p.Content,
                    p.LikeCount,
                    p.CommentCount,
                    Array.Empty<CommentResult>(),
                    p.CreatedAt,
                    p.UpdatedAt))
            .ToListAsync(cancellationToken);

        return await AttachCommentsAsync(posts, FeedCommentsPerPost, cancellationToken);
    }

    public async Task<IReadOnlyList<FeedPostResult>> GetGlobalFeedAsync(
        bool includeAllComments,
        CancellationToken cancellationToken)
    {
        var posts = await dbContext.Posts
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .Join(dbContext.Users,
                p => p.UserId,
                u => u.Id,
                (p, u) => new FeedPostResult(
                    p.Id,
                    new PostAuthorResult(u.Id, u.Username, u.UserType.ToString(), u.Profile.ProfileImageUrl),
                    p.Content,
                    p.LikeCount,
                    p.CommentCount,
                    Array.Empty<CommentResult>(),
                    p.CreatedAt,
                    p.UpdatedAt))
            .ToListAsync(cancellationToken);

        return await AttachCommentsAsync(
            posts,
            includeAllComments ? FeedCommentsPerPost : GuestCommentsPerPost,
            cancellationToken);
    }

    private async Task<IReadOnlyList<FeedPostResult>> AttachCommentsAsync(
        IReadOnlyList<FeedPostResult> posts,
        int? maxCommentsPerPost,
        CancellationToken cancellationToken)
    {
        if (posts.Count == 0)
        {
            return posts;
        }

        var postIds = posts.Select(p => p.Id).ToList();

        var comments = await dbContext.Comments
            .Where(c => postIds.Contains(c.PostId))
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .AsNoTracking()
            .Join(dbContext.Users,
                c => c.UserId,
                u => u.Id,
                (c, u) => new CommentResult(
                    c.Id,
                    c.PostId,
                    new PostAuthorResult(u.Id, u.Username, u.UserType.ToString(), u.Profile.ProfileImageUrl),
                    c.Content,
                    c.LikeCount,
                    c.CreatedAt,
                    c.UpdatedAt))
            .ToListAsync(cancellationToken);

        var commentsByPostId = comments
            .GroupBy(c => c.PostId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CommentResult>)(maxCommentsPerPost is null
                    ? g.ToList()
                    : g.TakeLast(maxCommentsPerPost.Value).ToList()));

        return posts
            .Select(post => post with
            {
                Comments = commentsByPostId.TryGetValue(post.Id, out var postComments)
                    ? postComments
                    : Array.Empty<CommentResult>(),
            })
            .ToList();
    }
}

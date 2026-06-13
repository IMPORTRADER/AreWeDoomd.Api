using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Common;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Domain.Comments;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class CommentRepository(AreWeDoomdDbContext dbContext) : ICommentRepository
{
    public Task<Comment?> GetByIdAsync(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        return dbContext.Comments
            .FirstOrDefaultAsync(
                x => x.PostId == postId && x.Id == commentId,
                cancellationToken);
    }

    public Task<Comment?> GetByIdWithLikesAsync(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        return dbContext.Comments
            .Include(x => x.Likes)
            .FirstOrDefaultAsync(
                x => x.PostId == postId && x.Id == commentId,
                cancellationToken);
    }

    public async Task<CommentResult?> GetByIdProjectedAsync(
        Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        return await dbContext.Comments
            .Where(x => x.PostId == postId && x.Id == commentId)
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
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommentResult>> GetByPostIdAsync(
        Guid postId, CancellationToken cancellationToken)
    {
        return await GetByPostIdProjectedAsync(postId, maxComments: null, cancellationToken);
    }

    public async Task<IReadOnlyList<CommentResult>> GetByPostIdProjectedAsync(
        Guid postId,
        int? maxComments,
        CancellationToken cancellationToken)
    {
        IQueryable<CommentResult> query = dbContext.Comments
            .Where(x => x.PostId == postId)
            .OrderBy(x => x.CreatedAt)
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
                    c.UpdatedAt));

        if (maxComments is not null)
        {
            query = query.Take(maxComments.Value);
        }

        return await query
            .ToListAsync(cancellationToken);
    }

    private IQueryable<CommentResult> ProjectComments(IQueryable<Comment> comments)
    {
        return comments
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
                    c.UpdatedAt));
    }

    public Task<int> CountByPostIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        return dbContext.Comments.CountAsync(c => c.PostId == postId, cancellationToken);
    }

    public Task<CommentCursor?> GetCursorAsync(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        return dbContext.Comments
            .Where(c => c.PostId == postId && c.Id == commentId)
            .Select(c => new CommentCursor(c.CreatedAt, c.Id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommentResult>> GetPageByPostIdAsync(
        Guid postId,
        int? take,
        bool descending,
        CancellationToken cancellationToken)
    {
        var source = dbContext.Comments.Where(c => c.PostId == postId);

        IQueryable<Comment> ordered = descending
            ? source.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            : source.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id);

        if (take is not null)
        {
            ordered = ordered.Take(take.Value);
        }

        return await ProjectComments(ordered).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommentResult>> GetOlderThanAsync(
        Guid postId,
        CommentCursor cursor,
        int take,
        CancellationToken cancellationToken)
    {
        var page = await ProjectComments(dbContext.Comments
                .Where(c => c.PostId == postId)
                .Where(c => c.CreatedAt < cursor.CreatedAt
                    || (c.CreatedAt == cursor.CreatedAt && c.Id < cursor.Id))
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                .Take(take))
            .ToListAsync(cancellationToken);

        page.Reverse();
        return page;
    }

    public async Task<IReadOnlyList<CommentResult>> GetFromAnchorAsync(
        Guid postId,
        CommentCursor cursor,
        int take,
        CancellationToken cancellationToken)
    {
        return await ProjectComments(dbContext.Comments
                .Where(c => c.PostId == postId)
                .Where(c => c.CreatedAt > cursor.CreatedAt
                    || (c.CreatedAt == cursor.CreatedAt && c.Id >= cursor.Id))
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                .Take(take))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommentResult>> GetNewerThanAsync(
        Guid postId,
        CommentCursor cursor,
        int take,
        CancellationToken cancellationToken)
    {
        return await ProjectComments(dbContext.Comments
                .Where(c => c.PostId == postId)
                .Where(c => c.CreatedAt > cursor.CreatedAt
                    || (c.CreatedAt == cursor.CreatedAt && c.Id > cursor.Id))
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                .Take(take))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsOlderAsync(Guid postId, CommentCursor cursor, CancellationToken cancellationToken)
    {
        return dbContext.Comments
            .Where(c => c.PostId == postId)
            .AnyAsync(c => c.CreatedAt < cursor.CreatedAt
                || (c.CreatedAt == cursor.CreatedAt && c.Id < cursor.Id), cancellationToken);
    }

    public Task<bool> ExistsNewerAsync(Guid postId, CommentCursor cursor, CancellationToken cancellationToken)
    {
        return dbContext.Comments
            .Where(c => c.PostId == postId)
            .AnyAsync(c => c.CreatedAt > cursor.CreatedAt
                || (c.CreatedAt == cursor.CreatedAt && c.Id > cursor.Id), cancellationToken);
    }

    public Task AddAsync(Comment comment, CancellationToken cancellationToken)
    {
        return dbContext.Comments.AddAsync(comment, cancellationToken).AsTask();
    }

    public Task UpdateAsync(Comment comment, CancellationToken cancellationToken)
    {
        dbContext.Comments.Update(comment);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Comment comment, CancellationToken cancellationToken)
    {
        dbContext.Comments.Remove(comment);
        return Task.CompletedTask;
    }
}

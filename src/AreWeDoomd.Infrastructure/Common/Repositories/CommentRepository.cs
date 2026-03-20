using AreWeDoomd.Application.Common.Interfaces;
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

    public async Task<IReadOnlyList<CommentResult>> GetByPostIdAsync(
        Guid postId, CancellationToken cancellationToken)
    {
        return await dbContext.Comments
            .Where(x => x.PostId == postId)
            .OrderBy(x => x.CreatedAt)
            .AsNoTracking()
            .Select(x => new CommentResult(
                x.Id,
                x.PostId,
                x.UserId,
                x.Content,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
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

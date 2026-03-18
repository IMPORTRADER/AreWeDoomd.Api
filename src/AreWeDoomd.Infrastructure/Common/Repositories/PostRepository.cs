using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Posts;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class PostRepository(AreWeDoomdDbContext dbContext) : IPostRepository
{
    public Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Posts
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<Post?> GetByIdWithLikesAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Posts
            .Include(p => p.Likes)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Posts
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Post post, CancellationToken cancellationToken)
    {
        return dbContext.Posts.AddAsync(post, cancellationToken).AsTask();
    }

    public Task UpdateAsync(Post post, CancellationToken cancellationToken)
    {
        dbContext.Posts.Update(post);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Post post, CancellationToken cancellationToken)
    {
        dbContext.Posts.Remove(post);
        return Task.CompletedTask;
    }
}

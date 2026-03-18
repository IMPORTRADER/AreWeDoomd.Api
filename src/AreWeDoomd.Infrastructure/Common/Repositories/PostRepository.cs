using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Posts;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class PostRepository(AreWeDoomdDbContext dbContext) : IPostRepository
{
    public async Task<IReadOnlyList<Post>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Posts
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

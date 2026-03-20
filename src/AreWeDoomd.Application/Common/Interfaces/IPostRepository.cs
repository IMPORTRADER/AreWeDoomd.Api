using AreWeDoomd.Domain.Posts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Post?> GetByIdWithCommentsAsync(Guid id, CancellationToken cancellationToken);
    Task<Post?> GetByIdWithLikesAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Post>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(Post post, CancellationToken cancellationToken);
    Task UpdateAsync(Post post, CancellationToken cancellationToken);
    Task DeleteAsync(Post post, CancellationToken cancellationToken);
}

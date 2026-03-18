using AreWeDoomd.Domain.Posts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IPostRepository
{
    Task<IReadOnlyList<Post>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}

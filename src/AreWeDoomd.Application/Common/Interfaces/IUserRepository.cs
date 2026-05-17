using AreWeDoomd.Application.Features.Search.Common;
using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<bool> IsUsernameTakenAsync(string username, Guid? excludeUserId, CancellationToken cancellationToken);
    Task<bool> IsEmailTakenAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchUserResult>> SearchByQueryAsync(string query, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task UpdateAsync(User user, CancellationToken cancellationToken);
}

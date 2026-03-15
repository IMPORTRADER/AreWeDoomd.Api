using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<bool> IsUsernameTakenAsync(string username, CancellationToken cancellationToken);
    Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task UpdateAsync(User user, CancellationToken cancellationToken);
}


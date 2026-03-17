using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class UserRepository(AreWeDoomdDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public Task<bool> IsUsernameTakenAsync(string username, CancellationToken cancellationToken)
        => dbContext.Users.AnyAsync(u => u.Username == username, cancellationToken);

    public Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken)
        => dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken)
        => dbContext.Users.AddAsync(user, cancellationToken).AsTask();

    public Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Update(user);
        return Task.CompletedTask;
    }
}


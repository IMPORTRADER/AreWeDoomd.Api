using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Search.Common;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class UserRepository(AreWeDoomdDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

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

    public async Task<IReadOnlyList<SearchUserResult>> SearchByQueryAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim();

        return await dbContext.Users
            .Where(u => EF.Functions.Like(u.Username, $"%{normalizedQuery}%"))
            .OrderBy(u => u.Username)
            .Take(50)
            .AsNoTracking()
            .Select(u => new SearchUserResult(
                u.Id,
                u.Username,
                u.UserType.ToString(),
                u.Profile.ProfileImageUrl,
                u.Profile.Biography))
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken)
        => dbContext.Users.AddAsync(user, cancellationToken).AsTask();

    public Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Update(user);
        return Task.CompletedTask;
    }
}

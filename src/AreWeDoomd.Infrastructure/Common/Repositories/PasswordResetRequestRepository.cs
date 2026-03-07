using System.Linq;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class PasswordResetRequestRepository(AreWeDoomdDbContext dbContext) : IPasswordResetRequestRepository
{
    public async Task<IReadOnlyList<PasswordResetRequest>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.PasswordResetRequests
            .Where(x => x.UserId == userId && !x.IsRevoked && x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<PasswordResetRequest?> GetLatestPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.PasswordResetRequests
            .Where(x => x.UserId == userId && !x.IsRevoked && x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddAsync(PasswordResetRequest request, CancellationToken cancellationToken)
        => dbContext.PasswordResetRequests.AddAsync(request, cancellationToken).AsTask();

    public Task UpdateAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        dbContext.PasswordResetRequests.Update(request);
        return Task.CompletedTask;
    }
}


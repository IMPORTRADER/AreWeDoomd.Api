using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IPasswordResetRequestRepository
{
    Task<IReadOnlyList<PasswordResetRequest>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<PasswordResetRequest?> GetLatestPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(PasswordResetRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(PasswordResetRequest request, CancellationToken cancellationToken);
}


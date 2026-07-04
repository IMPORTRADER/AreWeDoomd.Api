using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IBulkCreationRecordRepository
{
    Task AddAsync(BulkCreationRecord record, CancellationToken ct);
    Task<IReadOnlyList<BulkCreationRecord>> ListByJobAsync(Guid jobId, CancellationToken ct);
}

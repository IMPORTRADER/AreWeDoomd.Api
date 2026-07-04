using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class BulkCreationRecordRepository(AreWeDoomdDbContext dbContext) : IBulkCreationRecordRepository
{
    public async Task AddAsync(BulkCreationRecord record, CancellationToken ct)
    {
        await dbContext.BulkCreationRecords.AddAsync(record, ct);
    }

    public async Task<IReadOnlyList<BulkCreationRecord>> ListByJobAsync(Guid jobId, CancellationToken ct)
    {
        return await dbContext.BulkCreationRecords
            .AsNoTracking()
            .Where(r => r.JobId == jobId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Scheduling;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class SchedulingSettingsRepository(AreWeDoomdDbContext dbContext) : ISchedulingSettingsRepository
{
    public Task<SchedulingSettings?> GetAsync(CancellationToken ct)
    {
        return dbContext.SchedulingSettings.FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(SchedulingSettings settings, CancellationToken ct)
    {
        await dbContext.SchedulingSettings.AddAsync(settings, ct);
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Ai;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class LlmSettingsRepository(AreWeDoomdDbContext dbContext) : ILlmSettingsRepository
{
    public Task<LlmSettings?> GetAsync(CancellationToken ct)
    {
        return dbContext.LlmSettings.FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(LlmSettings settings, CancellationToken ct)
    {
        await dbContext.LlmSettings.AddAsync(settings, ct);
    }
}

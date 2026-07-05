using AreWeDoomd.Domain.Scheduling;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface ISchedulingSettingsRepository
{
    Task<SchedulingSettings?> GetAsync(CancellationToken ct);
    Task AddAsync(SchedulingSettings settings, CancellationToken ct);
}

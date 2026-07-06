using AreWeDoomd.Domain.Ai;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface ILlmSettingsRepository
{
    Task<LlmSettings?> GetAsync(CancellationToken ct);
    Task AddAsync(LlmSettings settings, CancellationToken ct);
}

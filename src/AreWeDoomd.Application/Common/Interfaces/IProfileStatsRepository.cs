using AreWeDoomd.Application.Features.Users.Common;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IProfileStatsRepository
{
    Task<ProfileStatsResult> GetStatsAsync(Guid userId, CancellationToken cancellationToken);
}

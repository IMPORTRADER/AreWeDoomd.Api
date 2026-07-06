using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAiFleetStats;

public sealed class GetAiFleetStatsQueryHandler(
    IAiUserReadRepository repository,
    IDecisionLogReader reader,
    IDateTimeProvider clock)
    : IRequestHandler<GetAiFleetStatsQuery, Result<AiFleetStatsResult>>
{
    public async Task<Result<AiFleetStatsResult>> Handle(GetAiFleetStatsQuery request, CancellationToken cancellationToken)
    {
        var nowUtc = clock.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);

        var (total, withPersonality) = await repository.CountAsync(cancellationToken);
        var dayStats = await reader.GetDailyStatsAsync(today, nowUtc, cancellationToken);

        if (dayStats is null)
        {
            return Result<AiFleetStatsResult>.Success(
                new AiFleetStatsResult(total, withPersonality, 0, 0, 0, 0, 0, false));
        }

        return Result<AiFleetStatsResult>.Success(
            new AiFleetStatsResult(
                total,
                withPersonality,
                dayStats.Total,
                dayStats.Executed,
                dayStats.Dropped,
                dayStats.Failed,
                dayStats.ActionsLastHour,
                true));
    }
}

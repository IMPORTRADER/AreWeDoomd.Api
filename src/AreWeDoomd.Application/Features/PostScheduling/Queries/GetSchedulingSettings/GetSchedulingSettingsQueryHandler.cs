using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.GetSchedulingSettings;

public sealed class GetSchedulingSettingsQueryHandler(
    ISchedulingSettingsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetSchedulingSettingsQuery, Result<SchedulingSettingsResult>>
{
    public async Task<Result<SchedulingSettingsResult>> Handle(
        GetSchedulingSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(cancellationToken);
        if (settings is null)
        {
            settings = SchedulingSettings.CreateDefault(dateTimeProvider.UtcNow);
        }

        return Result<SchedulingSettingsResult>.Success(new SchedulingSettingsResult(
            settings.DesireThreshold,
            settings.MaxPostsPerDay,
            settings.PostLengthGuide,
            (int)settings.LatePolicy,
            settings.LateGraceHours,
            (int)settings.Strategy,
            settings.UpdatedAt));
    }
}

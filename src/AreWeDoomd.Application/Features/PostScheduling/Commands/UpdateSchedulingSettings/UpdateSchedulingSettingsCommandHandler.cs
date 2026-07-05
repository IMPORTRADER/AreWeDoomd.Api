using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateSchedulingSettings;

public sealed class UpdateSchedulingSettingsCommandHandler(
    ISchedulingSettingsRepository repository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateSchedulingSettingsCommand, Result<SchedulingSettingsResult>>
{
    public async Task<Result<SchedulingSettingsResult>> Handle(
        UpdateSchedulingSettingsCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var settings = await repository.GetAsync(cancellationToken);
        if (settings is null)
        {
            settings = SchedulingSettings.CreateDefault(now);
            await repository.AddAsync(settings, cancellationToken);
        }

        settings.Update(
            request.DesireThreshold, request.MaxPostsPerDay, request.PostLengthGuide,
            (SchedulingLatePolicy)request.LatePolicy, request.LateGraceHours,
            (LlmSchedulingStrategy)request.Strategy, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SchedulingSettingsResult>.Success(new SchedulingSettingsResult(
            settings.DesireThreshold, settings.MaxPostsPerDay, settings.PostLengthGuide,
            (int)settings.LatePolicy, settings.LateGraceHours, (int)settings.Strategy, settings.UpdatedAt));
    }
}

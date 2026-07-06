using FluentValidation;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateSchedulingSettings;

public sealed class UpdateSchedulingSettingsCommandValidator : AbstractValidator<UpdateSchedulingSettingsCommand>
{
    public UpdateSchedulingSettingsCommandValidator()
    {
        RuleFor(x => x.DesireThreshold).InclusiveBetween(0, 100);
        RuleFor(x => x.MaxPostsPerDay).InclusiveBetween(1, 10);
        RuleFor(x => x.PostLengthGuide).InclusiveBetween(50, 10_000);
        RuleFor(x => x.LatePolicy).InclusiveBetween(0, 1);
        RuleFor(x => x.LateGraceHours).InclusiveBetween(0, 24);
        RuleFor(x => x.Strategy).InclusiveBetween(0, 1);
    }
}

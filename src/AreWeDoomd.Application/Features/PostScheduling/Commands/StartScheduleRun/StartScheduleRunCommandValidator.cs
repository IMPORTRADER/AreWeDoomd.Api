using FluentValidation;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.StartScheduleRun;

public sealed class StartScheduleRunCommandValidator : AbstractValidator<StartScheduleRunCommand>
{
    public StartScheduleRunCommandValidator()
    {
        RuleFor(x => x.TriggeredByUserId).NotEmpty();
        RuleFor(x => x.AiUserIds)
            .Must(ids => ids == null || ids.Count > 0)
            .WithMessage("AiUserIds must be null (all) or non-empty.");
    }
}

using FluentValidation;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.SubmitScheduleDecision;

public sealed class SubmitScheduleDecisionCommandValidator : AbstractValidator<SubmitScheduleDecisionCommand>
{
    public SubmitScheduleDecisionCommandValidator()
    {
        RuleFor(x => x.RunItemId).NotEmpty();
        RuleFor(x => x.CallerAiUserId).NotEmpty();
        RuleFor(x => x.Posts).NotNull();
        RuleForEach(x => x.Posts).ChildRules(post =>
        {
            post.RuleFor(p => p.Content).NotEmpty().MaximumLength(10_000);
        });
    }
}

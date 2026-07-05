using FluentValidation;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.CancelScheduledPost;

public sealed class CancelScheduledPostCommandValidator : AbstractValidator<CancelScheduledPostCommand>
{
    public CancelScheduledPostCommandValidator()
    {
        RuleFor(x => x.ScheduledPostId).NotEmpty();
    }
}

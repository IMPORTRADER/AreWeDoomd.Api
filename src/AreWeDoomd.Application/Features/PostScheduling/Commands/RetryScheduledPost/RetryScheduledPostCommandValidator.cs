using FluentValidation;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.RetryScheduledPost;

public sealed class RetryScheduledPostCommandValidator : AbstractValidator<RetryScheduledPostCommand>
{
    public RetryScheduledPostCommandValidator()
    {
        RuleFor(x => x.ScheduledPostId).NotEmpty();
    }
}

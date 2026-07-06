using FluentValidation;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateScheduledPost;

public sealed class UpdateScheduledPostCommandValidator : AbstractValidator<UpdateScheduledPostCommand>
{
    public UpdateScheduledPostCommandValidator()
    {
        RuleFor(x => x.ScheduledPostId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(10_000);
    }
}

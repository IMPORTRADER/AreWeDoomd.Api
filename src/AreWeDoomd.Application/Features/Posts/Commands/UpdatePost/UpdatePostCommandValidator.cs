using FluentValidation;

namespace AreWeDoomd.Application.Features.Posts.Commands.UpdatePost;

public sealed class UpdatePostCommandValidator : AbstractValidator<UpdatePostCommand>
{
    public UpdatePostCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(10_000);
    }
}

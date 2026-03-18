using FluentValidation;

namespace AreWeDoomd.Application.Features.Posts.Commands.CreatePost;

public sealed class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(10_000);
    }
}

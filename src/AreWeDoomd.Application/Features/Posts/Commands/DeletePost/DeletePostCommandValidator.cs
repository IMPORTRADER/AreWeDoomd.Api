using FluentValidation;

namespace AreWeDoomd.Application.Features.Posts.Commands.DeletePost;

public sealed class DeletePostCommandValidator : AbstractValidator<DeletePostCommand>
{
    public DeletePostCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

using FluentValidation;

namespace AreWeDoomd.Application.Features.PostLikes.Commands.UnlikePost;

public sealed class UnlikePostCommandValidator : AbstractValidator<UnlikePostCommand>
{
    public UnlikePostCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

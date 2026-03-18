using FluentValidation;

namespace AreWeDoomd.Application.Features.PostLikes.Commands.LikePost;

public sealed class LikePostCommandValidator : AbstractValidator<LikePostCommand>
{
    public LikePostCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

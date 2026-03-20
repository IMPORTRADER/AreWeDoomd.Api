using FluentValidation;

namespace AreWeDoomd.Application.Features.CommentLikes.Commands.LikeComment;

public sealed class LikeCommentCommandValidator : AbstractValidator<LikeCommentCommand>
{
    public LikeCommentCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();

        RuleFor(x => x.CommentId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

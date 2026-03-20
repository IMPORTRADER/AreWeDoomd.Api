using FluentValidation;

namespace AreWeDoomd.Application.Features.CommentLikes.Commands.UnlikeComment;

public sealed class UnlikeCommentCommandValidator : AbstractValidator<UnlikeCommentCommand>
{
    public UnlikeCommentCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();

        RuleFor(x => x.CommentId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

using FluentValidation;

namespace AreWeDoomd.Application.Features.CommentLikes.Queries.GetCommentLikes;

public sealed class GetCommentLikesQueryValidator : AbstractValidator<GetCommentLikesQuery>
{
    public GetCommentLikesQueryValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();

        RuleFor(x => x.CommentId)
            .NotEmpty();
    }
}

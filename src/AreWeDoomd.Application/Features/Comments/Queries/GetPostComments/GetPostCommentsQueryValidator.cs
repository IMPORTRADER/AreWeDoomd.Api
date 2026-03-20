using FluentValidation;

namespace AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;

public sealed class GetPostCommentsQueryValidator : AbstractValidator<GetPostCommentsQuery>
{
    public GetPostCommentsQueryValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();
    }
}

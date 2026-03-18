using FluentValidation;

namespace AreWeDoomd.Application.Features.Posts.Queries.GetPost;

public sealed class GetPostQueryValidator : AbstractValidator<GetPostQuery>
{
    public GetPostQueryValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();
    }
}

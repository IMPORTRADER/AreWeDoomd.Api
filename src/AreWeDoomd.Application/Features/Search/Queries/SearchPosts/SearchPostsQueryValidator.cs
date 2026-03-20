using FluentValidation;

namespace AreWeDoomd.Application.Features.Search.Queries.SearchPosts;

public sealed class SearchPostsQueryValidator : AbstractValidator<SearchPostsQuery>
{
    public SearchPostsQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .MaximumLength(100);
    }
}

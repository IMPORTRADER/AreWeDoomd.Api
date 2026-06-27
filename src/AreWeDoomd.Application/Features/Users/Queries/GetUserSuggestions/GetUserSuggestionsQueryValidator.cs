using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserSuggestions;

public sealed class GetUserSuggestionsQueryValidator : AbstractValidator<GetUserSuggestionsQuery>
{
    public GetUserSuggestionsQueryValidator()
    {
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

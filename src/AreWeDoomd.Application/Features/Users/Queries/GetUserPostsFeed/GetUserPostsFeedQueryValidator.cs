using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserPostsFeed;

public sealed class GetUserPostsFeedQueryValidator : AbstractValidator<GetUserPostsFeedQuery>
{
    public GetUserPostsFeedQueryValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

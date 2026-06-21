using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserLikedPostsFeed;

public sealed class GetUserLikedPostsFeedQueryValidator : AbstractValidator<GetUserLikedPostsFeedQuery>
{
    public GetUserLikedPostsFeedQueryValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

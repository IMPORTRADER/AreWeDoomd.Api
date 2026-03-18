using FluentValidation;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetFollowingFeed;

public sealed class GetFollowingFeedQueryValidator : AbstractValidator<GetFollowingFeedQuery>
{
    public GetFollowingFeedQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

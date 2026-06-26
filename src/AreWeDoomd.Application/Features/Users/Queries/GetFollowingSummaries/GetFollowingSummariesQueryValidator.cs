using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Queries.GetFollowingSummaries;

public sealed class GetFollowingSummariesQueryValidator : AbstractValidator<GetFollowingSummariesQuery>
{
    public GetFollowingSummariesQueryValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

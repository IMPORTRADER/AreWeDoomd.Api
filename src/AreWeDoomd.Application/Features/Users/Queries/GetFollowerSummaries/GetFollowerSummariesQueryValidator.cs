using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Queries.GetFollowerSummaries;

public sealed class GetFollowerSummariesQueryValidator : AbstractValidator<GetFollowerSummariesQuery>
{
    public GetFollowerSummariesQueryValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.RequesterId).NotEmpty();
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

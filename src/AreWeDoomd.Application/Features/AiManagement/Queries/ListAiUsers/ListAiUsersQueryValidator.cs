using FluentValidation;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.ListAiUsers;

public sealed class ListAiUsersQueryValidator : AbstractValidator<ListAiUsersQuery>
{
    public ListAiUsersQueryValidator()
    {
        RuleFor(q => q.Offset).GreaterThanOrEqualTo(0);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}

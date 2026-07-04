using FluentValidation;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentDecisions;

public sealed class GetAgentDecisionsQueryValidator : AbstractValidator<GetAgentDecisionsQuery>
{
    public GetAgentDecisionsQueryValidator()
    {
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);

        RuleFor(q => q.FromUtc)
            .Must((query, fromUtc) => fromUtc <= query.ToUtc)
            .When(q => q.FromUtc.HasValue && q.ToUtc.HasValue)
            .WithMessage("FromUtc must be less than or equal to ToUtc.");
    }
}

using FluentValidation;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentOpsLogs;

public sealed class GetAgentOpsLogsQueryValidator : AbstractValidator<GetAgentOpsLogsQuery>
{
    public GetAgentOpsLogsQueryValidator()
    {
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);

        RuleFor(q => q.FromUtc)
            .Must((query, fromUtc) => fromUtc <= query.ToUtc)
            .When(q => q.FromUtc.HasValue && q.ToUtc.HasValue)
            .WithMessage("FromUtc must be less than or equal to ToUtc.");
    }
}

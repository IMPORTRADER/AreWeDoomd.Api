using FluentValidation;

namespace AreWeDoomd.Application.Features.Agents.Queries.GetAgentPersona;

public sealed class GetAgentPersonaQueryValidator : AbstractValidator<GetAgentPersonaQuery>
{
    public GetAgentPersonaQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
    }
}

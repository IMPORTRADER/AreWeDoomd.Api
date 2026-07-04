using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Agents.Queries.GetAgentPersona;

public sealed class GetAgentPersonaQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetAgentPersonaQuery, Result<AgentPersonaResult>>
{
    public async Task<Result<AgentPersonaResult>> Handle(
        GetAgentPersonaQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<AgentPersonaResult>.NotFound("agent.not_found", "Agent not found.");
        }

        if (user.UserType != UserType.Ai)
        {
            return Result<AgentPersonaResult>.NotFound("agent.not_found", "Agent not found.");
        }

        if (user.AiPersonality is null)
        {
            return Result<AgentPersonaResult>.NotFound("agent.persona.none", "Agent has no personality configured.");
        }

        var result = new AgentPersonaResult(
            user.Id,
            user.AiPersonality.Traits,
            user.AiPersonality.TypingStyle,
            user.AiPersonality.Summary,
            user.AiPersonality.Version);

        return Result<AgentPersonaResult>.Success(result);
    }
}

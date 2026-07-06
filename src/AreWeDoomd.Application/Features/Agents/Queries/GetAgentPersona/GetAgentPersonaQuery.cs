using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Agents.Queries.GetAgentPersona;

public sealed record GetAgentPersonaQuery(Guid UserId) : IRequest<Result<AgentPersonaResult>>;

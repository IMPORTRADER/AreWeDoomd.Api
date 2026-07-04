using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentDecisions;

public sealed record GetAgentDecisionsQuery(
    Guid? AiUserId,
    string? Action,
    string? Outcome,
    DateOnly? FromUtc,
    DateOnly? ToUtc,
    string? Cursor,
    int PageSize)
    : IRequest<Result<AgentDecisionsResult>>;

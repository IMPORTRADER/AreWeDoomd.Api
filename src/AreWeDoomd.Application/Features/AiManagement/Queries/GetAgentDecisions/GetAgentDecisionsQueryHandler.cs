using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentDecisions;

public sealed class GetAgentDecisionsQueryHandler(IDecisionLogReader reader)
    : IRequestHandler<GetAgentDecisionsQuery, Result<AgentDecisionsResult>>
{
    public async Task<Result<AgentDecisionsResult>> Handle(GetAgentDecisionsQuery request, CancellationToken cancellationToken)
    {
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        var filter = new DecisionLogFilter(
            request.AiUserId?.ToString(),
            request.Action,
            request.Outcome,
            request.FromUtc,
            request.ToUtc);

        var page = await reader.ReadAsync(filter, request.Cursor, pageSize, cancellationToken);

        return Result<AgentDecisionsResult>.Success(
            new AgentDecisionsResult(page.Items, page.NextCursor, page.HasMore, page.LogAvailable));
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentOpsLogs;

public sealed class GetAgentOpsLogsQueryHandler(IAgentOpsLogReader reader)
    : IRequestHandler<GetAgentOpsLogsQuery, Result<AgentOpsLogsResult>>
{
    public async Task<Result<AgentOpsLogsResult>> Handle(GetAgentOpsLogsQuery request, CancellationToken cancellationToken)
    {
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        var filter = new AgentOpsLogFilter(
            request.Level,
            request.Source,
            request.AiUserId?.ToString(),
            request.FromUtc,
            request.ToUtc);

        var page = await reader.ReadAsync(filter, request.Cursor, pageSize, cancellationToken);

        return Result<AgentOpsLogsResult>.Success(
            new AgentOpsLogsResult(page.Items, page.NextCursor, page.HasMore, page.LogAvailable));
    }
}

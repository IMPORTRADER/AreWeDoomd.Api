using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentOpsLogs;

public sealed record GetAgentOpsLogsQuery(
    string? Level,
    string? Source,
    Guid? AiUserId,
    DateOnly? FromUtc,
    DateOnly? ToUtc,
    string? Cursor,
    int PageSize)
    : IRequest<Result<AgentOpsLogsResult>>;

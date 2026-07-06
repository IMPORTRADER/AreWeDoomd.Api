using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.ClearAgentOpsLogs;

public sealed record ClearAgentOpsLogsCommand() : IRequest<Result<int>>;

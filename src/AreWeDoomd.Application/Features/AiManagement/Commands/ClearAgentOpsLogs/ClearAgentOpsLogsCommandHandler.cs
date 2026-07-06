using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.ClearAgentOpsLogs;

public sealed class ClearAgentOpsLogsCommandHandler(IAgentOpsLogCleaner cleaner)
    : IRequestHandler<ClearAgentOpsLogsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ClearAgentOpsLogsCommand request, CancellationToken cancellationToken)
    {
        int deleted = await cleaner.ClearAsync(cancellationToken);
        return Result<int>.Success(deleted);
    }
}

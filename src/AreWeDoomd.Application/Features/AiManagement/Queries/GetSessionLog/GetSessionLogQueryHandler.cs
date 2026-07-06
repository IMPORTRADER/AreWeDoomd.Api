using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetSessionLog;

public sealed class GetSessionLogQueryHandler(ISessionLogReader reader)
    : IRequestHandler<GetSessionLogQuery, Result<SessionLogResult>>
{
    public async Task<Result<SessionLogResult>> Handle(GetSessionLogQuery request, CancellationToken cancellationToken)
    {
        var content = await reader.ReadAsync(request.SessionRef, cancellationToken);

        if (content is null)
        {
            return Result<SessionLogResult>.NotFound("session_log.not_found", "Session log not found.");
        }

        return Result<SessionLogResult>.Success(new SessionLogResult(content));
    }
}

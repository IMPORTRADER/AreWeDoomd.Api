using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetSessionLog;

public sealed record GetSessionLogQuery(string SessionRef)
    : IRequest<Result<SessionLogResult>>;

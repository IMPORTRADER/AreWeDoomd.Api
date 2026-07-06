using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.GetScheduleRun;

public sealed record GetScheduleRunQuery(Guid RunId) : IRequest<Result<ScheduleRunDetailResult>>;

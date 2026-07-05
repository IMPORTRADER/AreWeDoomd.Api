using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.ListScheduleRuns;

public sealed record ListScheduleRunsQuery(DateOnly Date, int Offset, int PageSize) : IRequest<Result<ScheduleRunListResult>>;

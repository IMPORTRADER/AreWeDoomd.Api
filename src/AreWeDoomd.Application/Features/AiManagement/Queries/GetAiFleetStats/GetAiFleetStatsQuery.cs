using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAiFleetStats;

public sealed record GetAiFleetStatsQuery() : IRequest<Result<AiFleetStatsResult>>;

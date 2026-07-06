using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.GetSchedulingSettings;

public sealed record GetSchedulingSettingsQuery : IRequest<Result<SchedulingSettingsResult>>;

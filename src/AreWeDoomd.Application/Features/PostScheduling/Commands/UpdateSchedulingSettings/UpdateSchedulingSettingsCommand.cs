using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateSchedulingSettings;

public sealed record UpdateSchedulingSettingsCommand(
    int DesireThreshold,
    int MaxPostsPerDay,
    int PostLengthGuide,
    int LatePolicy,
    int LateGraceHours,
    int Strategy) : IRequest<Result<SchedulingSettingsResult>>;

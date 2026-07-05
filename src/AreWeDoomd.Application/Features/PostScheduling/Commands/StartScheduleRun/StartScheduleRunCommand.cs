using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.StartScheduleRun;

public sealed record StartScheduleRunCommand(
    Guid TriggeredByUserId,
    IReadOnlyList<Guid>? AiUserIds,
    bool OverwriteExisting) : IRequest<Result<StartScheduleRunResult>>;

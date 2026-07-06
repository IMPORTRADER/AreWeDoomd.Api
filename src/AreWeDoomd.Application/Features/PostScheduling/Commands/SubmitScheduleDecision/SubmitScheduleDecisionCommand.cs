using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.SubmitScheduleDecision;

public sealed record SubmitScheduleDecisionCommand(
    Guid RunItemId,
    Guid CallerAiUserId,
    int DesireScore,
    string? Reasoning,
    int RequestedPostCount,
    string? ModelUsed,
    string? ErrorDetail,
    IReadOnlyList<SubmittedScheduledPost> Posts) : IRequest<Result<Unit>>;

namespace AreWeDoomd.Application.Features.PostScheduling.Common;

public sealed record ScheduleRunItemResult(
    Guid Id,
    Guid AiUserId,
    string Status,
    int? DesireScore,
    string? Reasoning,
    int? RequestedPostCount,
    int DroppedPostCount,
    string? ModelUsed,
    string? ErrorDetail,
    IReadOnlyList<ScheduledPostResult> Posts);

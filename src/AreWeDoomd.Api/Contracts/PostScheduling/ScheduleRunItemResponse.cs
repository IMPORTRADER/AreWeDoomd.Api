namespace AreWeDoomd.Api.Contracts.PostScheduling;

public sealed record ScheduleRunItemResponse(
    Guid Id,
    Guid AiUserId,
    string Status,
    int? DesireScore,
    string? Reasoning,
    int? RequestedPostCount,
    int DroppedPostCount,
    string? ModelUsed,
    string? ErrorDetail,
    List<ScheduledPostResponse> Posts);

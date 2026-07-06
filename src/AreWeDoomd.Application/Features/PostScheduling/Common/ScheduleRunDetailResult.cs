namespace AreWeDoomd.Application.Features.PostScheduling.Common;

public sealed record ScheduleRunDetailResult(
    Guid Id,
    DateOnly RunDate,
    int ThresholdSnapshot,
    int MaxPostsSnapshot,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ScheduleRunItemResult> Items);

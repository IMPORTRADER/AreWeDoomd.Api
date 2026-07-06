namespace AreWeDoomd.Api.Contracts.PostScheduling;

public sealed record ScheduleRunResponse(
    Guid Id,
    DateOnly RunDate,
    int ThresholdSnapshot,
    int MaxPostsSnapshot,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    List<ScheduleRunItemResponse> Items);

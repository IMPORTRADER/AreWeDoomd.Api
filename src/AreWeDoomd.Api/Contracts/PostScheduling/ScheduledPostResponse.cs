namespace AreWeDoomd.Api.Contracts.PostScheduling;

public sealed record ScheduledPostResponse(
    Guid Id,
    Guid? ScheduleRunItemId,
    Guid AiUserId,
    string Content,
    DateTimeOffset ScheduledAtUtc,
    string Status,
    bool WasTimeAdjusted,
    string? ErrorMessage,
    DateTimeOffset? PublishedAtUtc,
    Guid? PublishedPostId);

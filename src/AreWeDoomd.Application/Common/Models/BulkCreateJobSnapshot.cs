namespace AreWeDoomd.Application.Common.Models;

public sealed record BulkCreateJobSnapshot(
    Guid JobId,
    string Status,
    int Requested,
    int Generated,
    int Created,
    IReadOnlyList<BulkCreateFailedEntry> Failed,
    IReadOnlyList<string> CreatedUsers,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    bool Rebuilt = false);

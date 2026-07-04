namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record BulkCreateJobResponse(
    Guid JobId,
    string Status,
    int Requested,
    int Generated,
    int Created,
    IReadOnlyList<BulkCreateFailedEntryResponse> Failed,
    IReadOnlyList<string> CreatedUsers,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    bool Rebuilt);

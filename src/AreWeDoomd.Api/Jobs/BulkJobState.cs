using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Api.Jobs;

internal sealed class BulkJobState
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "queued";
    public int Requested { get; set; }
    public int Generated { get; set; }
    public int Created { get; set; }
    public List<BulkCreateFailedEntry> Failed { get; } = [];
    public List<string> CreatedUsers { get; } = [];
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public BulkCreateJobSnapshot ToSnapshot() =>
        new(JobId, Status, Requested, Generated, Created,
            Failed.AsReadOnly(), CreatedUsers.AsReadOnly(),
            StartedAt, FinishedAt, Rebuilt: false);
}

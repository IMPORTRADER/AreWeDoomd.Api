namespace AreWeDoomd.Application.Common.Interfaces;

public interface IBulkCreateJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default);
    ValueTask<Guid> DequeueAsync(CancellationToken ct);
}

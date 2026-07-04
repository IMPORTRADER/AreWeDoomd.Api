using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IBulkCreateJobStore
{
    void Create(Guid jobId, int requestedCount);
    BulkCreateJobSnapshot? TryGetSnapshot(Guid jobId);
}

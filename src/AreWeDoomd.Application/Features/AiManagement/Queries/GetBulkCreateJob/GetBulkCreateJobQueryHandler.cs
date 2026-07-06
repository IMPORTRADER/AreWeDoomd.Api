using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetBulkCreateJob;

public sealed class GetBulkCreateJobQueryHandler(
    IBulkCreateJobStore store,
    IBulkCreationRecordRepository recordRepository)
    : IRequestHandler<GetBulkCreateJobQuery, Result<BulkCreateJobSnapshot>>
{
    public async Task<Result<BulkCreateJobSnapshot>> Handle(
        GetBulkCreateJobQuery request, CancellationToken cancellationToken)
    {
        // Store hit — return live snapshot
        var snapshot = store.TryGetSnapshot(request.JobId);
        if (snapshot != null)
        {
            return Result<BulkCreateJobSnapshot>.Success(snapshot);
        }

        // DB rebuild — store lost it (e.g. server restart)
        var records = await recordRepository.ListByJobAsync(request.JobId, cancellationToken);
        if (records.Count == 0)
        {
            return Result<BulkCreateJobSnapshot>.NotFound(
                "bulk_job.not_found",
                $"Bulk job '{request.JobId}' was not found.");
        }

        var createdUsernames = records.Select(r => r.Username).ToList();

        // Known limitation of DB rebuild: Requested is set to the count of
        // successfully-created records because the original requested count and
        // any failure details are not persisted — only successes land in the DB.
        var rebuilt = new BulkCreateJobSnapshot(
            JobId: request.JobId,
            Status: "completed",
            Requested: createdUsernames.Count,
            Generated: createdUsernames.Count,
            Created: createdUsernames.Count,
            Failed: [],
            CreatedUsers: createdUsernames,
            StartedAt: null,
            FinishedAt: null,
            Rebuilt: true);

        return Result<BulkCreateJobSnapshot>.Success(rebuilt);
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetBulkCreateJob;

public sealed class GetBulkCreateJobQueryHandler(
    IBulkCreateJobStore store,
    IAiUserReadRepository repository)
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
        var createdUsers = await repository.ListUsernamesByBulkJobAsync(request.JobId, cancellationToken);
        if (createdUsers.Count == 0)
        {
            return Result<BulkCreateJobSnapshot>.NotFound(
                "bulk_job.not_found",
                $"Bulk job '{request.JobId}' was not found.");
        }

        var rebuilt = new BulkCreateJobSnapshot(
            JobId: request.JobId,
            Status: "completed",
            Requested: createdUsers.Count,
            Generated: createdUsers.Count,
            Created: createdUsers.Count,
            Failed: [],
            CreatedUsers: createdUsers,
            StartedAt: null,
            FinishedAt: null,
            Rebuilt: true);

        return Result<BulkCreateJobSnapshot>.Success(rebuilt);
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.StartBulkCreateAiUsers;

public sealed class StartBulkCreateAiUsersCommandHandler(
    IBulkCreateJobQueue queue,
    IBulkCreateJobStore store)
    : IRequestHandler<StartBulkCreateAiUsersCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        StartBulkCreateAiUsersCommand request, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        store.Create(jobId, request.Count);
        await queue.EnqueueAsync(jobId, cancellationToken);

        return Result<Guid>.Success(jobId);
    }
}

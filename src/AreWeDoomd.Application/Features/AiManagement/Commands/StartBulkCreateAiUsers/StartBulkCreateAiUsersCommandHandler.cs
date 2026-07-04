using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.StartBulkCreateAiUsers;

public sealed class StartBulkCreateAiUsersCommandHandler(
    IPersonaGenerator generator,
    IBulkCreateJobQueue queue,
    IBulkCreateJobStore store)
    : IRequestHandler<StartBulkCreateAiUsersCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        StartBulkCreateAiUsersCommand request, CancellationToken cancellationToken)
    {
        if (!generator.IsConfigured)
        {
            return Result<Guid>.Failure(
                "persona.generator_unconfigured",
                "The persona generator is not configured. Please set a valid API key for the configured provider.");
        }

        var jobId = Guid.NewGuid();
        store.Create(jobId, request.Count);
        await queue.EnqueueAsync(jobId, cancellationToken);

        return Result<Guid>.Success(jobId);
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.RetryScheduledPost;

public sealed class RetryScheduledPostCommandHandler(
    IScheduledPostRepository repository,
    IUnitOfWork unitOfWork,
    ISchedulePublisherWaker publisherWaker)
    : IRequestHandler<RetryScheduledPostCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(RetryScheduledPostCommand request, CancellationToken cancellationToken)
    {
        var post = await repository.GetByIdAsync(request.ScheduledPostId, cancellationToken);
        if (post is null)
        {
            return Result<Unit>.NotFound("scheduling.post_not_found", "Scheduled post not found.");
        }

        if (!post.ResetForRetry())
        {
            return Result<Unit>.Conflict("scheduling.not_failed", "Only failed posts can be retried.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        publisherWaker.Wake();
        return Result<Unit>.Success(Unit.Value);
    }
}

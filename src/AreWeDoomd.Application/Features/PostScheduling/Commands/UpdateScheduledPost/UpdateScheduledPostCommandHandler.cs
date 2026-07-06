using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateScheduledPost;

public sealed class UpdateScheduledPostCommandHandler(
    IScheduledPostRepository repository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    ISchedulePublisherWaker publisherWaker)
    : IRequestHandler<UpdateScheduledPostCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(UpdateScheduledPostCommand request, CancellationToken cancellationToken)
    {
        var post = await repository.GetByIdAsync(request.ScheduledPostId, cancellationToken);
        if (post is null)
        {
            return Result<Unit>.NotFound("scheduling.post_not_found", "Scheduled post not found.");
        }

        var now = dateTimeProvider.UtcNow;
        if (request.ScheduledAtUtc <= now)
        {
            return Result<Unit>.Failure("scheduling.time_in_past", "Scheduled time must be in the future.");
        }

        if (!post.UpdatePending(request.Content, request.ScheduledAtUtc, now))
        {
            return Result<Unit>.Conflict("scheduling.not_pending", "Only pending posts can be edited.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        publisherWaker.Wake();
        return Result<Unit>.Success(Unit.Value);
    }
}

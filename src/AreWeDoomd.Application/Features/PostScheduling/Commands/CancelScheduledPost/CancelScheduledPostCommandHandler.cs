using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.CancelScheduledPost;

public sealed class CancelScheduledPostCommandHandler(
    IScheduledPostRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelScheduledPostCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(CancelScheduledPostCommand request, CancellationToken cancellationToken)
    {
        var post = await repository.GetByIdAsync(request.ScheduledPostId, cancellationToken);
        if (post is null)
        {
            return Result<Unit>.NotFound("scheduling.post_not_found", "Scheduled post not found.");
        }

        if (!post.Cancel())
        {
            return Result<Unit>.Conflict("scheduling.not_pending", "Only pending posts can be cancelled.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}

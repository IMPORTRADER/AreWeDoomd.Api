using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.UnfollowUser;

public sealed class UnfollowUserCommandHandler(
    IUserFollowRepository userFollowRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UnfollowUserCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UnfollowUserCommand request, CancellationToken cancellationToken)
    {
        var follow = await userFollowRepository.GetAsync(
            request.FollowerId, request.FollowingId, cancellationToken);

        if (follow is null)
        {
            return Result<bool>.NotFound("follow.not_found", "You are not following this user.");
        }

        await userFollowRepository.DeleteAsync(follow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.UnfollowUser;

public sealed class UnfollowUserCommandHandler(
    IUserRepository userRepository,
    IUserFollowRepository userFollowRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UnfollowUserCommand, Result<FollowStateResult>>
{
    public async Task<Result<FollowStateResult>> Handle(UnfollowUserCommand request, CancellationToken cancellationToken)
    {
        var target = await userRepository.GetByUsernameAsync(request.TargetUsername, cancellationToken);
        if (target is null)
        {
            return Result<FollowStateResult>.NotFound("user.not_found", "User not found.");
        }

        var follow = await userFollowRepository.GetAsync(request.FollowerId, target.Id, cancellationToken);
        if (follow is not null)
        {
            await userFollowRepository.DeleteAsync(follow, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var followerCount = await userFollowRepository.CountFollowersAsync(target.Id, cancellationToken);
        return Result<FollowStateResult>.Success(new FollowStateResult(false, followerCount));
    }
}

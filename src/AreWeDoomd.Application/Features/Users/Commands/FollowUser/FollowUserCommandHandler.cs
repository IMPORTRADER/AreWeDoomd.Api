using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.FollowUser;

public sealed class FollowUserCommandHandler(
    IUserRepository userRepository,
    IUserFollowRepository userFollowRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<FollowUserCommand, Result<FollowStateResult>>
{
    public async Task<Result<FollowStateResult>> Handle(FollowUserCommand request, CancellationToken cancellationToken)
    {
        var target = await userRepository.GetByUsernameAsync(request.TargetUsername, cancellationToken);
        if (target is null)
        {
            return Result<FollowStateResult>.NotFound("user.not_found", "User not found.");
        }

        if (target.Id == request.FollowerId)
        {
            return Result<FollowStateResult>.NotFound("follow.self", "You cannot follow yourself.");
        }

        var alreadyFollowing = await userFollowRepository.ExistsAsync(
            request.FollowerId, target.Id, cancellationToken);

        if (!alreadyFollowing)
        {
            var follow = UserFollow.Create(request.FollowerId, target.Id, dateTimeProvider.UtcNow);
            await userFollowRepository.AddAsync(follow, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var followerCount = await userFollowRepository.CountFollowersAsync(target.Id, cancellationToken);
        return Result<FollowStateResult>.Success(new FollowStateResult(true, followerCount));
    }
}

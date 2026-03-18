using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.FollowUser;

public sealed class FollowUserCommandHandler(
    IUserRepository userRepository,
    IUserFollowRepository userFollowRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<FollowUserCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(FollowUserCommand request, CancellationToken cancellationToken)
    {
        if (request.FollowerId == request.FollowingId)
        {
            return Result<bool>.Failure("follow.self", "You cannot follow yourself.");
        }

        var targetUser = await userRepository.GetByIdAsync(request.FollowingId, cancellationToken);

        if (targetUser is null)
        {
            return Result<bool>.NotFound("user.not_found", "User not found.");
        }

        var alreadyFollowing = await userFollowRepository.ExistsAsync(
            request.FollowerId, request.FollowingId, cancellationToken);

        if (alreadyFollowing)
        {
            return Result<bool>.Conflict("follow.already_following", "You are already following this user.");
        }

        var now = dateTimeProvider.UtcNow;
        var follow = UserFollow.Create(request.FollowerId, request.FollowingId, now);

        await userFollowRepository.AddAsync(follow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

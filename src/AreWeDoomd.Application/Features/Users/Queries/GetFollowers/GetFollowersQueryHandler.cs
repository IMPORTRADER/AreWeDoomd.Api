using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetFollowers;

public sealed class GetFollowersQueryHandler(
    IUserRepository userRepository,
    IUserFollowRepository userFollowRepository)
    : IRequestHandler<GetFollowersQuery, Result<IReadOnlyList<FollowUserResult>>>
{
    public async Task<Result<IReadOnlyList<FollowUserResult>>> Handle(
        GetFollowersQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<IReadOnlyList<FollowUserResult>>.NotFound("user.not_found", "User not found.");
        }

        if (request.RequesterId != request.UserId)
        {
            var isFollowing = await userFollowRepository.ExistsAsync(
                request.RequesterId, request.UserId, cancellationToken);

            if (!isFollowing)
            {
                return Result<IReadOnlyList<FollowUserResult>>.Forbidden(
                    "follow.not_following", "You must follow this user to view their followers.");
            }
        }

        var followers = await userFollowRepository.GetFollowersAsync(request.UserId, cancellationToken);

        return Result<IReadOnlyList<FollowUserResult>>.Success(followers);
    }
}

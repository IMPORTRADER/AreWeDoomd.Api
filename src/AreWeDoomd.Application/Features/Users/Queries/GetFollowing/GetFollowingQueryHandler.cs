using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetFollowing;

public sealed class GetFollowingQueryHandler(
    IUserRepository userRepository,
    IUserFollowRepository userFollowRepository)
    : IRequestHandler<GetFollowingQuery, Result<IReadOnlyList<FollowUserResult>>>
{
    public async Task<Result<IReadOnlyList<FollowUserResult>>> Handle(
        GetFollowingQuery request, CancellationToken cancellationToken)
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
                    "follow.not_following", "You must follow this user to view their following list.");
            }
        }

        var following = await userFollowRepository.GetFollowingAsync(request.UserId, cancellationToken);

        return Result<IReadOnlyList<FollowUserResult>>.Success(following);
    }
}

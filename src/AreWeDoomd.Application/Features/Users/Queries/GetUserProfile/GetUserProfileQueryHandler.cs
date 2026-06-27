using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserProfile;

public sealed class GetUserProfileQueryHandler(
    IUserRepository userRepository,
    IUserFollowRepository userFollowRepository,
    IProfileStatsRepository profileStatsRepository)
    : IRequestHandler<GetUserProfileQuery, Result<UserProfileDetailResult>>
{
    public async Task<Result<UserProfileDetailResult>> Handle(
        GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, cancellationToken);

        if (user is null)
        {
            return Result<UserProfileDetailResult>.NotFound("user.not_found", "User not found.");
        }

        var isMe = request.RequesterId == user.Id;

        var isFollowedByMe = false;
        if (request.RequesterId is { } requesterId && !isMe)
        {
            isFollowedByMe = await userFollowRepository.ExistsAsync(requesterId, user.Id, cancellationToken);
        }

        var stats = await profileStatsRepository.GetStatsAsync(user.Id, cancellationToken);

        var detail = new UserProfileDetailResult(
            user.Id,
            user.Username,
            user.UserType.ToString(),
            user.Profile.Biography,
            user.Profile.ProfileImageUrl,
            user.CreatedAt,
            stats,
            ProfileBadgeCatalog.MockFor(user.Id),
            isFollowedByMe,
            isMe);

        return Result<UserProfileDetailResult>.Success(detail);
    }
}

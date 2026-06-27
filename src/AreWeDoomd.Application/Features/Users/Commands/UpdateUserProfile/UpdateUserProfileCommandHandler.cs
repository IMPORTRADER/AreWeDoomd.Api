using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetUserProfile;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandHandler(
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    IProfileStatsRepository profileStatsRepository)
    : IRequestHandler<UpdateUserProfileCommand, Result<UserProfileDetailResult>>
{
    public async Task<Result<UserProfileDetailResult>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<UserProfileDetailResult>.NotFound("user.not_found", "User not found.");
        }

        var now = dateTimeProvider.UtcNow;

        if (request.Username is not null)
        {
            var normalizedUsername = request.Username.Trim();

            if (await userRepository.IsUsernameTakenAsync(normalizedUsername, request.UserId, cancellationToken))
            {
                return Result<UserProfileDetailResult>.Conflict("user.username_taken", "Username is already taken.");
            }

            user.ChangeUsername(normalizedUsername, now);
        }

        if (request.Email is not null)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            if (await userRepository.IsEmailTakenAsync(normalizedEmail, request.UserId, cancellationToken))
            {
                return Result<UserProfileDetailResult>.Conflict("user.email_taken", "Email is already registered.");
            }

            user.ChangeEmail(normalizedEmail, now);
        }

        if (request.Biography is not null)
        {
            user.Profile.ChangeBiography(request.Biography, now);
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var stats = await profileStatsRepository.GetStatsAsync(user.Id, cancellationToken);

        return Result<UserProfileDetailResult>.Success(new UserProfileDetailResult(
            user.Id,
            user.Username,
            user.UserType.ToString(),
            user.Profile.Biography,
            user.Profile.ProfileImageUrl,
            user.CreatedAt,
            stats,
            ProfileBadgeCatalog.MockFor(user.Id),
            IsFollowedByMe: false,
            IsMe: true));
    }
}

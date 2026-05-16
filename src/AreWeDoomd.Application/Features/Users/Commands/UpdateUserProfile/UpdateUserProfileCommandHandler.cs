using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandHandler(
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateUserProfileCommand, Result<UserProfileResult>>
{
    public async Task<Result<UserProfileResult>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<UserProfileResult>.NotFound("user.not_found", "User not found.");
        }

        var now = dateTimeProvider.UtcNow;

        if (request.Username is not null)
        {
            var normalizedUsername = request.Username.Trim();

            if (await userRepository.IsUsernameTakenAsync(normalizedUsername, request.UserId, cancellationToken))
            {
                return Result<UserProfileResult>.Conflict("user.username_taken", "Username is already taken.");
            }

            user.ChangeUsername(normalizedUsername, now);
        }

        if (request.Email is not null)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            if (await userRepository.IsEmailTakenAsync(normalizedEmail, request.UserId, cancellationToken))
            {
                return Result<UserProfileResult>.Conflict("user.email_taken", "Email is already registered.");
            }

            user.ChangeEmail(normalizedEmail, now);
        }

        if (request.Biography is not null)
        {
            user.Profile.ChangeBiography(request.Biography, now);
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserProfileResult>.Success(
            new UserProfileResult(
                user.Id,
                user.Username,
                user.Email,
                user.UserType.ToString(),
                user.Profile.ProfileImageUrl,
                user.Profile.Biography));
    }
}

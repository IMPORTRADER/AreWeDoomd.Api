using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.UpdateProfileImage;

public sealed class UpdateProfileImageCommandHandler(
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProfileImageCommand, Result<UserProfileResult>>
{
    public async Task<Result<UserProfileResult>> Handle(UpdateProfileImageCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<UserProfileResult>.NotFound("user.not_found", "User not found.");
        }

        var now = dateTimeProvider.UtcNow;
        user.Profile.ChangeProfileImage(request.ProfileImageUrl, now);

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

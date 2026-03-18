using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ChangePasswordCommand, Result<ChangePasswordResult>>
{
    public async Task<Result<ChangePasswordResult>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<ChangePasswordResult>.NotFound("user.not_found", "User not found.");
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            return Result<ChangePasswordResult>.Failure("user.invalid_password", "Current password is incorrect.");
        }

        var now = dateTimeProvider.UtcNow;
        user.SetPassword(passwordHasher.Hash(request.NewPassword), now);

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenGenerator.Generate(user);

        return Result<ChangePasswordResult>.Success(
            new ChangePasswordResult(accessToken, "Password changed successfully."));
    }
}

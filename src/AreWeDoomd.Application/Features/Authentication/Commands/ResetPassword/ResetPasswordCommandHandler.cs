using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IDateTimeProvider dateTimeProvider,
    IPasswordResetRequestRepository passwordResetRequestRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ResetPasswordCommand, Result<AuthResult>>
{
    public async Task<Result<AuthResult>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return Result<AuthResult>.NotFound("auth.user_not_found", "User not found.");
        }

        var now = dateTimeProvider.UtcNow;

        var resetRequest = await passwordResetRequestRepository
            .GetLatestPendingByUserIdAsync(user.Id, cancellationToken);

        if (resetRequest is null || !resetRequest.IsActive(now))
        {
            return Result<AuthResult>.Failure("auth.reset_code_invalid", "Reset code is invalid or expired.");
        }

        if (!passwordHasher.Verify(resetRequest.CodeHash, request.Code))
        {
            return Result<AuthResult>.Failure("auth.reset_code_invalid", "Reset code is invalid.");
        }

        user.SetPassword(passwordHasher.Hash(request.NewPassword), now);
        resetRequest.Consume(now);

        await userRepository.UpdateAsync(user, cancellationToken);
        await passwordResetRequestRepository.UpdateAsync(resetRequest, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthResult>.Success(
            new AuthResult(
                user.Id,
                user.Username,
                user.Email,
                user.UserType,
                accessTokenGenerator.Generate(user)));
    }
}

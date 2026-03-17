using AreWeDoomd.Application.Common.Exceptions;
using AreWeDoomd.Application.Common.Interfaces;
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
    : IRequestHandler<ResetPasswordCommand, AuthResult>
{
    public async Task<AuthResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        var now = dateTimeProvider.UtcNow;

        var resetRequest = await passwordResetRequestRepository
            .GetLatestPendingByUserIdAsync(user.Id, cancellationToken);

        if (resetRequest is null || !resetRequest.IsActive(now))
        {
            throw new InvalidOperationException("Reset code is invalid or expired.");
        }

        if (!passwordHasher.Verify(resetRequest.CodeHash, request.Code))
        {
            throw new InvalidOperationException("Reset code is invalid.");
        }

        user.SetPassword(passwordHasher.Hash(request.NewPassword), now);
        resetRequest.Consume(now);

        await userRepository.UpdateAsync(user, cancellationToken);
        await passwordResetRequestRepository.UpdateAsync(resetRequest, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResult(
            user.Id,
            user.Username,
            user.Email,
            user.UserType,
            accessTokenGenerator.Generate(user));
    }
}


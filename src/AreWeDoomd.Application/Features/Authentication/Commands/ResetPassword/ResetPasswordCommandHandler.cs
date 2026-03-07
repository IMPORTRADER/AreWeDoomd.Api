using AreWeDoomd.Application.Common.Exceptions;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IPasswordResetRequestRepository passwordResetRequestRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ResetPasswordCommand, AuthResult>
{
    public async Task<AuthResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("Code is required.", nameof(request.Code));
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw new ArgumentException("New password must be at least 8 characters.", nameof(request.NewPassword));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

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
            user.UserType);
    }
}


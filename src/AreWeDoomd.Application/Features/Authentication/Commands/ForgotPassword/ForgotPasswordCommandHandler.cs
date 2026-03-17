using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IPasswordResetCodeGenerator codeGenerator,
    IDateTimeProvider dateTimeProvider,
    IEmailSender emailSender,
    IPasswordResetRequestRepository passwordResetRequestRepository,
    IPasswordResetSettings passwordResetSettings,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ForgotPasswordCommand, Result<ForgotPasswordResult>>
{
    public async Task<Result<ForgotPasswordResult>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return Result<ForgotPasswordResult>.NotFound("auth.user_not_found", "User not found.");
        }

        var now = dateTimeProvider.UtcNow;

        var activeRequests = await passwordResetRequestRepository
            .GetPendingByUserIdAsync(user.Id, cancellationToken);

        foreach (var reset in activeRequests.Where(x => x.IsActive(now)))
        {
            reset.Revoke(now);
            await passwordResetRequestRepository.UpdateAsync(reset, cancellationToken);
        }

        var digits = Math.Max(4, passwordResetSettings.CodeLength);
        var lifetime = TimeSpan.FromMinutes(Math.Max(1, passwordResetSettings.LifetimeMinutes));
        var code = codeGenerator.Generate(digits);
        var resetRequest = PasswordResetRequest.Create(user.Id, passwordHasher.Hash(code), now, lifetime);

        await passwordResetRequestRepository.AddAsync(resetRequest, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var message = new EmailMessage(
            user.Email,
            "Password reset code",
            $"Your reset code is {code}. It expires at {resetRequest.ExpiresAt:O}.");

        await emailSender.SendAsync(message, cancellationToken);

        return Result<ForgotPasswordResult>.Success(
            new ForgotPasswordResult(code, resetRequest.ExpiresAt));
    }
}

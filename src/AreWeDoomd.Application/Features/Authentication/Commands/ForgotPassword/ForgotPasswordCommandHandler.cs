using AreWeDoomd.Application.Common.Exceptions;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
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
    IClientContextAccessor clientContextAccessor,
    IPasswordResetRequestRepository passwordResetRequestRepository,
    IPasswordResetSettings passwordResetSettings,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    public async Task<ForgotPasswordResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        var clientContext = await clientContextAccessor.GetCurrentAsync(cancellationToken);

        if (user.UserType != clientContext.UserType)
        {
            throw new ClientAuthenticationException("Client is not allowed to manage this user type.");
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

        return new ForgotPasswordResult(code, resetRequest.ExpiresAt);
    }
}

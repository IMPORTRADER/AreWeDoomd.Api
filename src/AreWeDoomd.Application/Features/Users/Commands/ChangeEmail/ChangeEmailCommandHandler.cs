using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.ChangeEmail;

public sealed class ChangeEmailCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ChangeEmailCommand, Result<ChangeEmailResult>>
{
    public async Task<Result<ChangeEmailResult>> Handle(ChangeEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<ChangeEmailResult>.NotFound("user.not_found", "User not found.");
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            return Result<ChangeEmailResult>.Failure("user.invalid_password", "Current password is incorrect.");
        }

        var normalizedEmail = request.NewEmail.Trim().ToLowerInvariant();

        if (await userRepository.IsEmailTakenAsync(normalizedEmail, user.Id, cancellationToken))
        {
            return Result<ChangeEmailResult>.Conflict("user.email_taken", "Email is already registered.");
        }

        var now = dateTimeProvider.UtcNow;
        user.ChangeEmail(normalizedEmail, now);

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenGenerator.Generate(user);

        return Result<ChangeEmailResult>.Success(
            new ChangeEmailResult(accessToken, "Email changed successfully."));
    }
}

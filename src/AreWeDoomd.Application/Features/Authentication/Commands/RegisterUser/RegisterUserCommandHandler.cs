using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterUserCommand, Result<AuthResult>>
{
    public async Task<Result<AuthResult>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedUsername = request.Username.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await userRepository.IsUsernameTakenAsync(normalizedUsername, null, cancellationToken))
        {
            return Result<AuthResult>.Conflict("auth.username_taken", "Username is already taken.");
        }

        if (await userRepository.IsEmailTakenAsync(normalizedEmail, null, cancellationToken))
        {
            return Result<AuthResult>.Conflict("auth.email_taken", "Email is already registered.");
        }

        var now = dateTimeProvider.UtcNow;

        var user = User.Create(
            normalizedUsername,
            normalizedEmail,
            passwordHasher.Hash(request.Password),
            request.UserType,
            now);

        await userRepository.AddAsync(user, cancellationToken);
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

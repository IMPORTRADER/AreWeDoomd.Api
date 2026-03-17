using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterUserCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedUsername = request.Username.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await userRepository.IsUsernameTakenAsync(normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException("Username is already taken.");
        }

        if (await userRepository.IsEmailTakenAsync(normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email is already registered.");
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

        return new AuthResult(
            user.Id,
            user.Username,
            user.Email,
            user.UserType,
            accessTokenGenerator.Generate(user));
    }
}

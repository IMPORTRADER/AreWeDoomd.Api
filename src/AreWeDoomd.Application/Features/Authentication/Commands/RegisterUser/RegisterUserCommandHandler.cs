using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IUserTokenFactory userTokenFactory,
    IClientContextAccessor clientContextAccessor,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterUserCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username is required.", nameof(request.Username));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(request.Password));
        }

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

        var clientContext = await clientContextAccessor.GetCurrentAsync(cancellationToken);
        var now = dateTimeProvider.UtcNow;

        var user = User.Create(
            normalizedUsername,
            normalizedEmail,
            passwordHasher.Hash(request.Password),
            clientContext.UserType,
            now);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var authToken = await userTokenFactory.CreateAsync(user, clientContext, cancellationToken);

        return new AuthResult(
            user.Id,
            user.Username,
            user.Email,
            user.UserType,
            authToken.AccessToken,
            authToken.ExpiresAt);
    }
}


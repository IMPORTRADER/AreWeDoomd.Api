using AreWeDoomd.Application.Common.Exceptions;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.LoginUser;

public sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUserTokenFactory userTokenFactory,
    IClientContextAccessor clientContextAccessor)
    : IRequestHandler<LoginUserCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.", nameof(request.Password));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Invalid credentials.");
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new NotFoundException("Invalid credentials.");
        }

        var clientContext = await clientContextAccessor.GetCurrentAsync(cancellationToken);

        if (user.UserType != clientContext.UserType)
        {
            throw new ClientAuthenticationException("Client is not allowed to sign in this user type.");
        }

        var token = await userTokenFactory.CreateAsync(user, clientContext, cancellationToken);

        return new AuthResult(
            user.Id,
            user.Username,
            user.Email,
            user.UserType,
            token.AccessToken,
            token.ExpiresAt);
    }
}


using AreWeDoomd.Application.Common.Exceptions;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.LoginUser;

public sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator)
    : IRequestHandler<LoginUserCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Invalid credentials.");
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new NotFoundException("Invalid credentials.");
        }

        return new AuthResult(
            user.Id,
            user.Username,
            user.Email,
            user.UserType,
            accessTokenGenerator.Generate(user));
    }
}

using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.LoginUser;

public sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator)
    : IRequestHandler<LoginUserCommand, Result<AuthResult>>
{
    public async Task<Result<AuthResult>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return Result<AuthResult>.Failure("auth.invalid_credentials", "Invalid credentials.");
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return Result<AuthResult>.Failure("auth.invalid_credentials", "Invalid credentials.");
        }

        return Result<AuthResult>.Success(
            new AuthResult(
                user.Id,
                user.Username,
                user.Email,
                user.UserType,
                accessTokenGenerator.Generate(user)));
    }
}

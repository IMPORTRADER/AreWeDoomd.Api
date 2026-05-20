using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;

[SensitiveProperties]
public sealed record RegisterUserCommand(
    string Username,
    string Email,
    string Password,
    UserType UserType) : IRequest<Result<AuthResult>>;

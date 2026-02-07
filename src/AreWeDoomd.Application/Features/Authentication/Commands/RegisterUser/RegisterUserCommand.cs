using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;

public sealed record RegisterUserCommand(string Username, string Email, string Password) : IRequest<AuthResult>;


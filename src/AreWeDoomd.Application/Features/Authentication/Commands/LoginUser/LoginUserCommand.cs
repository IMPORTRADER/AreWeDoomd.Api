using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.LoginUser;

public sealed record LoginUserCommand(string Username, string Password) : IRequest<AuthResult>;


using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.LoginUser;

[SensitiveProperties]
public sealed record LoginUserCommand(string Username, string Password) : IRequest<Result<AuthResult>>;

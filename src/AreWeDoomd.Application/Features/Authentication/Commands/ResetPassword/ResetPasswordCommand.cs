using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Username, string Code, string NewPassword) : IRequest<AuthResult>;


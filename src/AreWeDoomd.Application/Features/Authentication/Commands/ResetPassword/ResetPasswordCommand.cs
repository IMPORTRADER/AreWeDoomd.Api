using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ResetPassword;

[SensitiveProperties]
public sealed record ResetPasswordCommand(string Username, string Code, string NewPassword) : IRequest<Result<AuthResult>>;

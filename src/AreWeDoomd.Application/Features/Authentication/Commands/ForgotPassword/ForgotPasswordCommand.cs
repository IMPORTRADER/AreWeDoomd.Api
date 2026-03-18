using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Username) : IRequest<Result<ForgotPasswordResult>>;

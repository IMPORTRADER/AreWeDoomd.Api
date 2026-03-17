using AreWeDoomd.Application.Features.Authentication.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Username) : IRequest<ForgotPasswordResult>;

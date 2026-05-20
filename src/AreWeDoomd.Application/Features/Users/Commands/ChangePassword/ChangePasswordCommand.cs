using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.ChangePassword;

[SensitiveProperties]
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result<ChangePasswordResult>>;

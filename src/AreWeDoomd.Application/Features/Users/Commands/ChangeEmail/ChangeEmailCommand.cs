using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.ChangeEmail;

[SensitiveProperties]
public sealed record ChangeEmailCommand(
    Guid UserId,
    string CurrentPassword,
    string NewEmail) : IRequest<Result<ChangeEmailResult>>;

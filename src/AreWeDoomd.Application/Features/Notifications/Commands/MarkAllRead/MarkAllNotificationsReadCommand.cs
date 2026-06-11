using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Notifications.Commands.MarkAllRead;

public sealed record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<Result<int>>;

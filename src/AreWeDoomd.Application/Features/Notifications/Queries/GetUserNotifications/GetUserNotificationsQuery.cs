using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Notifications.Dispatching;
using MediatR;

namespace AreWeDoomd.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed record GetUserNotificationsQuery(Guid UserId)
    : IRequest<Result<IReadOnlyList<UserNotificationDto>>>;

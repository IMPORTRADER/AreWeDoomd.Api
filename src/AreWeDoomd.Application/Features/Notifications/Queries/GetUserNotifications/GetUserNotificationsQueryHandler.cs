using System.Text.Json;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Notifications.Dispatching;
using AreWeDoomd.Domain.Notifications;
using MediatR;

namespace AreWeDoomd.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed class GetUserNotificationsQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetUserNotificationsQuery, Result<IReadOnlyList<UserNotificationDto>>>
{
    private const int MaxNotifications = 10;

    public async Task<Result<IReadOnlyList<UserNotificationDto>>> Handle(
        GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var entities = await notificationRepository.GetRecentAsync(
            request.UserId, MaxNotifications, cancellationToken);

        IReadOnlyList<UserNotificationDto> dtos = entities.Select(Map).ToList();

        return Result<IReadOnlyList<UserNotificationDto>>.Success(dtos);
    }

    private static UserNotificationDto Map(Notification notification)
    {
        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(notification.ParamsJson)
            ?? new Dictionary<string, string>();

        return new UserNotificationDto(
            notification.Id,
            notification.Template,
            parameters,
            notification.ActorName,
            notification.ActorType,
            notification.CreatedAt,
            notification.IsRead);
    }
}

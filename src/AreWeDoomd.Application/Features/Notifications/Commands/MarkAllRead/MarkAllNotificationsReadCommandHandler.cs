using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Notifications.Commands.MarkAllRead;

public sealed class MarkAllNotificationsReadCommandHandler(
    INotificationRepository notificationRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MarkAllNotificationsReadCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var updated = await notificationRepository.MarkAllReadAsync(
            request.UserId, dateTimeProvider.UtcNow, cancellationToken);

        return Result<int>.Success(updated);
    }
}

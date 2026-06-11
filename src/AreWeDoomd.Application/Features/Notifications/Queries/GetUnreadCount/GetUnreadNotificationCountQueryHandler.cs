using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Notifications.Queries.GetUnreadCount;

public sealed class GetUnreadNotificationCountQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    public async Task<Result<int>> Handle(
        GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var count = await notificationRepository.GetUnreadCountAsync(request.UserId, cancellationToken);
        return Result<int>.Success(count);
    }
}

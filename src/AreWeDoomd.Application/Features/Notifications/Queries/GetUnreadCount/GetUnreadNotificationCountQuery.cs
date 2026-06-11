using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Notifications.Queries.GetUnreadCount;

public sealed record GetUnreadNotificationCountQuery(Guid UserId) : IRequest<Result<int>>;

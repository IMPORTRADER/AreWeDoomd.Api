using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetFollowing;

public sealed record GetFollowingQuery(
    Guid UserId,
    Guid RequesterId) : IRequest<Result<IReadOnlyList<FollowUserResult>>>;

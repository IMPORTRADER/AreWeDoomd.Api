using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetFollowers;

public sealed record GetFollowersQuery(
    Guid UserId,
    Guid RequesterId) : IRequest<Result<IReadOnlyList<FollowUserResult>>>;

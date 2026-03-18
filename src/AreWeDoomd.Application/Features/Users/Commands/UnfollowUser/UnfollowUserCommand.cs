using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.UnfollowUser;

public sealed record UnfollowUserCommand(
    Guid FollowerId,
    Guid FollowingId) : IRequest<Result<bool>>;

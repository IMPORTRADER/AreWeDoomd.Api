using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.FollowUser;

public sealed record FollowUserCommand(
    Guid FollowerId,
    Guid FollowingId) : IRequest<Result<bool>>;

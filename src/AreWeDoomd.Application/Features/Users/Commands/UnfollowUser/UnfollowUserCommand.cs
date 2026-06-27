using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.UnfollowUser;

public sealed record UnfollowUserCommand(
    Guid FollowerId,
    string TargetUsername) : IRequest<Result<FollowStateResult>>;

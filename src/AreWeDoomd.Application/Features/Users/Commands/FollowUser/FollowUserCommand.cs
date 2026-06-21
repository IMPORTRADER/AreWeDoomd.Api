using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.FollowUser;

public sealed record FollowUserCommand(
    Guid FollowerId,
    string TargetUsername) : IRequest<Result<FollowStateResult>>;

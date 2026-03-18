using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostLikes.Commands.LikePost;

public sealed record LikePostCommand(
    Guid PostId,
    Guid UserId) : IRequest<Result<bool>>;

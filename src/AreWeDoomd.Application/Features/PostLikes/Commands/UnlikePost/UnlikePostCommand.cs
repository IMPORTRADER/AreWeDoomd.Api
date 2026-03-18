using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostLikes.Commands.UnlikePost;

public sealed record UnlikePostCommand(
    Guid PostId,
    Guid UserId) : IRequest<Result<bool>>;

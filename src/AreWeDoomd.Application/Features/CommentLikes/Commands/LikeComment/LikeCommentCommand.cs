using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.CommentLikes.Commands.LikeComment;

public sealed record LikeCommentCommand(
    Guid PostId,
    Guid CommentId,
    Guid UserId) : IRequest<Result<bool>>;

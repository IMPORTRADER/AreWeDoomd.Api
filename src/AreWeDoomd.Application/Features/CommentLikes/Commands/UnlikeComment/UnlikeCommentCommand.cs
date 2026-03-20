using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.CommentLikes.Commands.UnlikeComment;

public sealed record UnlikeCommentCommand(
    Guid PostId,
    Guid CommentId,
    Guid UserId) : IRequest<Result<bool>>;

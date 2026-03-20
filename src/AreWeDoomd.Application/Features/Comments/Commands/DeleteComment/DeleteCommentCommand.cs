using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Commands.DeleteComment;

public sealed record DeleteCommentCommand(
    Guid PostId,
    Guid CommentId,
    Guid UserId) : IRequest<Result<bool>>;

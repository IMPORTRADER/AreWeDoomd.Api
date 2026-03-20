using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Commands.UpdateComment;

public sealed record UpdateCommentCommand(
    Guid PostId,
    Guid CommentId,
    Guid UserId,
    string Content) : IRequest<Result<CommentResult>>;

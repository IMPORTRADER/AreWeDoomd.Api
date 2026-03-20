using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Commands.CreateComment;

public sealed record CreateCommentCommand(
    Guid PostId,
    Guid UserId,
    string Content) : IRequest<Result<CommentResult>>;

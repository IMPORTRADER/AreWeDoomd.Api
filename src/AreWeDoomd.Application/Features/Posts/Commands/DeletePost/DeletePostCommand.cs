using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Posts.Commands.DeletePost;

public sealed record DeletePostCommand(
    Guid PostId,
    Guid UserId) : IRequest<Result<bool>>;

using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Posts.Commands.UpdatePost;

public sealed record UpdatePostCommand(
    Guid PostId,
    Guid UserId,
    string Content) : IRequest<Result<PostResult>>;

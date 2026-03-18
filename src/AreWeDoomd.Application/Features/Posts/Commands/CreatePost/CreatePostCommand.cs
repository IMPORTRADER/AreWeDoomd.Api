using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Posts.Commands.CreatePost;

public sealed record CreatePostCommand(
    Guid UserId,
    string Content) : IRequest<Result<PostResult>>;

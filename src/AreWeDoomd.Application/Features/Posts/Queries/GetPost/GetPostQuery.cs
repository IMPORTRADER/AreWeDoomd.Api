using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Posts.Queries.GetPost;

public sealed record GetPostQuery(Guid PostId) : IRequest<Result<PostResult>>;

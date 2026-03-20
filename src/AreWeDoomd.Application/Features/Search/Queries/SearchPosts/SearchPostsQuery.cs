using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Search.Queries.SearchPosts;

public sealed record SearchPostsQuery(string Query) : IRequest<Result<IReadOnlyList<PostResult>>>;

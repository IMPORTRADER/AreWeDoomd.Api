using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Search.Queries.SearchPosts;

public sealed class SearchPostsQueryHandler(IPostRepository postRepository)
    : IRequestHandler<SearchPostsQuery, Result<IReadOnlyList<PostResult>>>
{
    public async Task<Result<IReadOnlyList<PostResult>>> Handle(
        SearchPostsQuery request,
        CancellationToken cancellationToken)
    {
        var posts = await postRepository.SearchByQueryAsync(request.Query, cancellationToken);

        return Result<IReadOnlyList<PostResult>>.Success(posts);
    }
}

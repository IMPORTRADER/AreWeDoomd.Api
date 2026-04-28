using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Posts.Queries.GetPost;

public sealed class GetPostQueryHandler(IPostRepository postRepository)
    : IRequestHandler<GetPostQuery, Result<PostResult>>
{
    public async Task<Result<PostResult>> Handle(GetPostQuery request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdProjectedAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<PostResult>.NotFound("post.not_found", "Post not found.");
        }

        return Result<PostResult>.Success(post);
    }
}

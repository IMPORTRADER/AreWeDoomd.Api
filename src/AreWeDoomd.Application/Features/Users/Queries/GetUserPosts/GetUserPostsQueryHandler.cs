using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserPosts;

public sealed class GetUserPostsQueryHandler(
    IUserRepository userRepository,
    IPostRepository postRepository)
    : IRequestHandler<GetUserPostsQuery, Result<IReadOnlyList<UserPostResult>>>
{
    public async Task<Result<IReadOnlyList<UserPostResult>>> Handle(GetUserPostsQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<IReadOnlyList<UserPostResult>>.NotFound("user.not_found", "User not found.");
        }

        var posts = await postRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        var results = posts
            .Select(p => new UserPostResult(
                p.Id,
                p.Content,
                p.LikeCount,
                p.CommentCount,
                p.CreatedAt,
                p.UpdatedAt))
            .ToList();

        return Result<IReadOnlyList<UserPostResult>>.Success(results);
    }
}

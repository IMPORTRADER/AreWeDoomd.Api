using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Posts;
using AreWeDoomd.Api.Contracts.Search;
using AreWeDoomd.Application.Features.Posts.Common;
using AreWeDoomd.Application.Features.Search.Common;
using AreWeDoomd.Application.Features.Search.Queries.SearchPosts;
using AreWeDoomd.Application.Features.Search.Queries.SearchUsers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController(IMediator mediator) : ControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType(typeof(List<SearchUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<SearchUserResponse>>> SearchUsers(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new SearchUsersQuery(query ?? string.Empty),
            cancellationToken);

        return this.ToActionResult(result, MapUsers);
    }

    [HttpGet("posts")]
    [ProducesResponseType(typeof(List<PostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<PostResponse>>> SearchPosts(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new SearchPostsQuery(query ?? string.Empty),
            cancellationToken);

        return this.ToActionResult(result, MapPosts);
    }

    private static List<SearchUserResponse> MapUsers(IReadOnlyList<SearchUserResult> users)
    {
        return users.Select(u => new SearchUserResponse(
            u.UserId,
            u.Username,
            u.ProfileImageUrl,
            u.Biography)).ToList();
    }

    private static List<PostResponse> MapPosts(IReadOnlyList<PostResult> posts)
    {
        return posts.Select(p => new PostResponse(
            p.Id,
            p.UserId,
            p.Content,
            p.LikeCount,
            p.CommentCount,
            p.CreatedAt,
            p.UpdatedAt)).ToList();
    }
}

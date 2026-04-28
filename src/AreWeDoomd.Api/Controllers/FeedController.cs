using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Contracts.Posts;
using AreWeDoomd.Application.Features.Feed.Queries.GetFollowingFeed;
using AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/feed")]
public sealed class FeedController(IMediator mediator) : ControllerBase
{
    [HttpGet("following")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<PostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PostResponse>>> GetFollowingFeed(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new GetFollowingFeedQuery(userId), cancellationToken);

        return this.ToActionResult(result, MapPosts);
    }

    [HttpGet("global")]
    [ProducesResponseType(typeof(IReadOnlyList<PostResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PostResponse>>> GetGlobalFeed(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGlobalFeedQuery(), cancellationToken);

        return this.ToActionResult(result, MapPosts);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }

    private static IReadOnlyList<PostResponse> MapPosts(IReadOnlyList<PostResult> results)
    {
        return results.Select(r => new PostResponse(
            r.Id,
            new PostAuthor(r.Author.UserId, r.Author.Username, r.Author.UserType, r.Author.ProfileImageUrl),
            r.Content,
            r.LikeCount,
            r.CommentCount,
            r.CreatedAt,
            r.UpdatedAt)).ToList();
    }
}

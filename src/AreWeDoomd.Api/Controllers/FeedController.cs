using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Feed;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Feed.Common;
using AreWeDoomd.Application.Features.Feed.Queries.GetFollowingFeed;
using AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;
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
    [ProducesResponseType(typeof(IReadOnlyList<FeedPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<FeedPostResponse>>> GetFollowingFeed(
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
    [ProducesResponseType(typeof(IReadOnlyList<FeedPostResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeedPostResponse>>> GetGlobalFeed(
        CancellationToken cancellationToken)
    {
        var includeAllComments = User.Identity?.IsAuthenticated == true;
        var result = await mediator.Send(new GetGlobalFeedQuery(includeAllComments), cancellationToken);

        return this.ToActionResult(result, MapPosts);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }

    private static IReadOnlyList<FeedPostResponse> MapPosts(IReadOnlyList<FeedPostResult> results)
    {
        return results.Select(r => new FeedPostResponse(
            r.Id,
            new PostAuthor(r.Author.UserId, r.Author.Username, r.Author.UserType, r.Author.ProfileImageUrl),
            r.Content,
            r.LikeCount,
            r.CommentCount,
            r.Comments.Select(MapComment).ToList(),
            r.CreatedAt,
            r.UpdatedAt)).ToList();
    }

    private static CommentResponse MapComment(CommentResult result)
    {
        return new CommentResponse(
            result.Id,
            result.PostId,
            new PostAuthor(result.Author.UserId, result.Author.Username, result.Author.UserType, result.Author.ProfileImageUrl),
            result.Content,
            result.LikeCount,
            result.CreatedAt,
            result.UpdatedAt);
    }
}

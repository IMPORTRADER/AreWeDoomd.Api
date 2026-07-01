using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.PostLikes;
using AreWeDoomd.Application.Features.PostLikes.Commands.LikePost;
using AreWeDoomd.Application.Features.PostLikes.Commands.UnlikePost;
using AreWeDoomd.Application.Features.PostLikes.Common;
using AreWeDoomd.Application.Features.PostLikes.Queries.GetPostLikes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/posts")]
[Authorize]
public sealed class PostLikesController(IMediator mediator) : ControllerBase
{
    [HttpPost("{postId}/likes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> LikePost(Guid postId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new LikePostCommand(postId, userId), cancellationToken);

        return this.ToNoContentResult(result);
    }

    [HttpDelete("{postId}/likes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> UnlikePost(Guid postId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new UnlikePostCommand(postId, userId), cancellationToken);

        return this.ToNoContentResult(result);
    }

    [HttpGet("{postId}/likes")]
    [ProducesResponseType(typeof(List<PostLikeUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<PostLikeUserResponse>>> GetPostLikes(
        Guid postId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetPostLikesQuery(postId), cancellationToken);

        return this.ToActionResult(result, MapLikes);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }

    private static List<PostLikeUserResponse> MapLikes(IReadOnlyList<PostLikeUserResult> likes)
    {
        return likes.Select(l => new PostLikeUserResponse(
            l.UserId,
            l.Username,
            l.UserType,
            l.ProfileImageUrl,
            l.LikedAt)).ToList();
    }
}

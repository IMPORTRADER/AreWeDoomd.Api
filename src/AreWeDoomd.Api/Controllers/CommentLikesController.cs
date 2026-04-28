using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.CommentLikes;
using AreWeDoomd.Application.Features.CommentLikes.Commands.LikeComment;
using AreWeDoomd.Application.Features.CommentLikes.Commands.UnlikeComment;
using AreWeDoomd.Application.Features.CommentLikes.Common;
using AreWeDoomd.Application.Features.CommentLikes.Queries.GetCommentLikes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/posts")]
[Authorize]
public sealed class CommentLikesController(IMediator mediator) : ControllerBase
{
    [HttpPost("{postId:guid}/comments/{commentId:guid}/likes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> LikeComment(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new LikeCommentCommand(postId, commentId, userId),
            cancellationToken);

        return this.ToNoContentResult(result);
    }

    [HttpDelete("{postId:guid}/comments/{commentId:guid}/likes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> UnlikeComment(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new UnlikeCommentCommand(postId, commentId, userId),
            cancellationToken);

        return this.ToNoContentResult(result);
    }

    [HttpGet("{postId:guid}/comments/{commentId:guid}/likes")]
    [ProducesResponseType(typeof(List<CommentLikeUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CommentLikeUserResponse>>> GetCommentLikes(
        Guid postId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetCommentLikesQuery(postId, commentId),
            cancellationToken);

        return this.ToActionResult(result, MapLikes);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }

    private static List<CommentLikeUserResponse> MapLikes(IReadOnlyList<CommentLikeUserResult> likes)
    {
        return likes.Select(l => new CommentLikeUserResponse(
            l.UserId,
            l.Username,
            l.UserType,
            l.ProfileImageUrl,
            l.LikedAt)).ToList();
    }
}

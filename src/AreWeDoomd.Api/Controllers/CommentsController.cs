using System.Security.Claims;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Filters;
using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Application.Features.Comments.Commands.CreateComment;
using AreWeDoomd.Application.Features.Comments.Commands.DeleteComment;
using AreWeDoomd.Application.Features.Comments.Commands.UpdateComment;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class CommentsController(IMediator mediator) : ControllerBase
{
    [HttpPost("{postId:guid}/comments")]
    [Authorize]
    [PublishActivity(
        ActivityType.CommentCreated,
        ActorType.Human,
        ActivityObjectType.Comment, objectIdParam: null,
        ActivityTargetType.Post,   targetIdParam: "postId")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CommentResponse>> CreateComment(
        Guid postId,
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new CreateCommentCommand(postId, userId, request.Content),
            cancellationToken);

        return this.ToActionResult(result, MapComment);
    }

    [HttpGet("{postId:guid}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CommentResponse>>> GetPostComments(
        Guid postId,
        CancellationToken cancellationToken)
    {
        var includeAllComments = User.Identity?.IsAuthenticated == true;
        var result = await mediator.Send(
            new GetPostCommentsQuery(postId, includeAllComments),
            cancellationToken);

        return this.ToActionResult(result, MapComments);
    }

    [HttpPatch("{postId:guid}/comments/{commentId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CommentResponse>> UpdateComment(
        Guid postId,
        Guid commentId,
        [FromBody] UpdateCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new UpdateCommentCommand(postId, commentId, userId, request.Content),
            cancellationToken);

        return this.ToActionResult(result, MapComment);
    }

    [HttpDelete("{postId:guid}/comments/{commentId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> DeleteComment(
        Guid postId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new DeleteCommentCommand(postId, commentId, userId),
            cancellationToken);

        return this.ToNoContentResult(result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
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

    private static List<CommentResponse> MapComments(IReadOnlyList<CommentResult> results)
    {
        return results.Select(MapComment).ToList();
    }
}

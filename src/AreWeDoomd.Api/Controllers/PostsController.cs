using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Contracts.Posts;
using AreWeDoomd.Application.Features.Posts.Commands.CreatePost;
using AreWeDoomd.Application.Features.Posts.Commands.DeletePost;
using AreWeDoomd.Application.Features.Posts.Commands.UpdatePost;
using AreWeDoomd.Application.Features.Posts.Common;
using AreWeDoomd.Application.Features.Posts.Queries.GetPost;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PostsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PostResponse>> Create(
        [FromBody] CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new CreatePostCommand(userId, request.Content),
            cancellationToken);

        return this.ToActionResult(result, MapPost);
    }

    [HttpGet("{postId:guid}")]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostResponse>> Get(
        Guid postId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPostQuery(postId), cancellationToken);

        return this.ToActionResult(result, MapPost);
    }

    [HttpPatch("{postId:guid}")]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PostResponse>> Update(
        Guid postId,
        [FromBody] UpdatePostRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new UpdatePostCommand(postId, userId, request.Content),
            cancellationToken);

        return this.ToActionResult(result, MapPost);
    }

    [HttpDelete("{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Delete(
        Guid postId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new DeletePostCommand(postId, userId),
            cancellationToken);

        return this.ToNoContentResult(result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }

    private static PostResponse MapPost(PostResult result)
    {
        return new PostResponse(
            result.Id,
            new PostAuthor(result.Author.UserId, result.Author.Username, result.Author.UserType, result.Author.ProfileImageUrl),
            result.Content,
            result.LikeCount,
            result.CommentCount,
            result.CreatedAt,
            result.UpdatedAt);
    }
}

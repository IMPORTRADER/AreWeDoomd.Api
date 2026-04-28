using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Users;
using AreWeDoomd.Application.Features.Users.Commands.FollowUser;
using AreWeDoomd.Application.Features.Users.Commands.UnfollowUser;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetFollowers;
using AreWeDoomd.Application.Features.Users.Queries.GetFollowing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class FollowsController(IMediator mediator) : ControllerBase
{
    [HttpPost("{userId:guid}/follow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Follow(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new FollowUserCommand(currentUserId, userId), cancellationToken);

        return this.ToNoContentResult(result);
    }

    [HttpDelete("{userId:guid}/follow")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Unfollow(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new UnfollowUserCommand(currentUserId, userId), cancellationToken);

        return this.ToNoContentResult(result);
    }

    [HttpGet("me/followers")]
    [ProducesResponseType(typeof(List<FollowUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<FollowUserResponse>>> GetMyFollowers(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new GetFollowersQuery(userId, userId), cancellationToken);

        return this.ToActionResult(result, MapFollowUsers);
    }

    [HttpGet("me/following")]
    [ProducesResponseType(typeof(List<FollowUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<FollowUserResponse>>> GetMyFollowing(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new GetFollowingQuery(userId, userId), cancellationToken);

        return this.ToActionResult(result, MapFollowUsers);
    }

    [HttpGet("{userId:guid}/followers")]
    [ProducesResponseType(typeof(List<FollowUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<FollowUserResponse>>> GetFollowers(
        Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new GetFollowersQuery(userId, currentUserId), cancellationToken);

        return this.ToActionResult(result, MapFollowUsers);
    }

    [HttpGet("{userId:guid}/following")]
    [ProducesResponseType(typeof(List<FollowUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<FollowUserResponse>>> GetFollowing(
        Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new GetFollowingQuery(userId, currentUserId), cancellationToken);

        return this.ToActionResult(result, MapFollowUsers);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }

    private static List<FollowUserResponse> MapFollowUsers(IReadOnlyList<FollowUserResult> users)
    {
        return users.Select(u => new FollowUserResponse(
            u.UserId,
            u.Username,
            u.UserType,
            u.ProfileImageUrl,
            u.FollowedAt)).ToList();
    }
}

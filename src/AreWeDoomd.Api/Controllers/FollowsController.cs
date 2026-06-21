using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Users;
using AreWeDoomd.Application.Features.Users.Commands.FollowUser;
using AreWeDoomd.Application.Features.Users.Commands.UnfollowUser;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetFollowers;
using AreWeDoomd.Application.Features.Users.Queries.GetFollowing;
using AreWeDoomd.Application.Features.Users.Queries.GetFollowerSummaries;
using AreWeDoomd.Application.Features.Users.Queries.GetFollowingSummaries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class FollowsController(IMediator mediator) : ControllerBase
{
    [HttpPost("{username}/follow")]
    [ProducesResponseType(typeof(FollowStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FollowStateResponse>> Follow(string username, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new FollowUserCommand(currentUserId, username), cancellationToken);
        return this.ToActionResult(result, s => new FollowStateResponse(s.IsFollowedByMe, s.FollowerCount));
    }

    [HttpDelete("{username}/follow")]
    [ProducesResponseType(typeof(FollowStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FollowStateResponse>> Unfollow(string username, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new UnfollowUserCommand(currentUserId, username), cancellationToken);
        return this.ToActionResult(result, s => new FollowStateResponse(s.IsFollowedByMe, s.FollowerCount));
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

    [HttpGet("{username}/followers")]
    [ProducesResponseType(typeof(UserSummaryPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSummaryPageResponse>> GetFollowerSummaries(
        string username,
        [FromQuery] int offset = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var requesterId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new GetFollowerSummariesQuery(username, requesterId, offset, pageSize), cancellationToken);
        return this.ToActionResult(result, MapSummaryPage);
    }

    [HttpGet("{username}/following")]
    [ProducesResponseType(typeof(UserSummaryPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSummaryPageResponse>> GetFollowingSummaries(
        string username,
        [FromQuery] int offset = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var requesterId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new GetFollowingSummariesQuery(username, requesterId, offset, pageSize), cancellationToken);
        return this.ToActionResult(result, MapSummaryPage);
    }

    private static UserSummaryPageResponse MapSummaryPage(UserSummaryPageResult page)
        => new(page.Items.Select(i => new UserSummaryResponse(
            i.UserId, i.Username, i.UserType, i.ProfileImageUrl, i.Bio, i.IsFollowedByMe)).ToList(),
            page.HasMore);

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

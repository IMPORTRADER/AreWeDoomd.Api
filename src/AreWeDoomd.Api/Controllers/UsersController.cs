using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Users;
using AreWeDoomd.Application.Features.Users.Commands.ChangePassword;
using AreWeDoomd.Application.Features.Users.Commands.UpdateProfileImage;
using AreWeDoomd.Application.Features.Users.Commands.UpdateUserProfile;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetUserPosts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpPatch("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new UpdateUserProfileCommand(userId, request.Username, request.Email, request.Biography),
            cancellationToken);

        return this.ToActionResult(result, MapProfile);
    }

    [HttpPatch("me/profile-image")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfileImage(
        [FromBody] UpdateProfileImageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new UpdateProfileImageCommand(userId, request.ProfileImageUrl),
            cancellationToken);

        return this.ToActionResult(result, MapProfile);
    }

    [HttpPatch("me/password")]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken);

        return this.ToActionResult(result, r => new ChangePasswordResponse(r.AccessToken, r.Message));
    }

    [HttpGet("me/posts")]
    [ProducesResponseType(typeof(List<UserPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<UserPostResponse>>> GetMyPosts(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new GetUserPostsQuery(userId), cancellationToken);

        return this.ToActionResult(result, MapPosts);
    }

    [HttpGet("{userId:guid}/posts")]
    [ProducesResponseType(typeof(List<UserPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<UserPostResponse>>> GetUserPosts(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserPostsQuery(userId), cancellationToken);

        return this.ToActionResult(result, MapPosts);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }

    private static UserProfileResponse MapProfile(UserProfileResult result)
    {
        return new UserProfileResponse(
            result.UserId,
            result.Username,
            result.Email,
            result.UserType,
            result.ProfileImageUrl,
            result.Biography);
    }

    private static List<UserPostResponse> MapPosts(IReadOnlyList<UserPostResult> posts)
    {
        return posts.Select(p => new UserPostResponse(
            p.Id,
            p.UserType,
            p.Content,
            p.LikeCount,
            p.CommentCount,
            p.CreatedAt,
            p.UpdatedAt)).ToList();
    }
}

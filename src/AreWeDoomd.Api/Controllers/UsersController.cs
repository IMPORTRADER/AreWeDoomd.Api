using System.Security.Claims;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Contracts.Feed;
using AreWeDoomd.Api.Contracts.Users;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Common;
using AreWeDoomd.Application.Features.Feed.Common;
using AreWeDoomd.Application.Features.Users.Commands.ChangePassword;
using AreWeDoomd.Application.Features.Users.Commands.UpdateProfileImage;
using AreWeDoomd.Application.Features.Users.Commands.UpdateUserProfile;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetUserLikedPostsFeed;
using AreWeDoomd.Application.Features.Users.Queries.GetUserPosts;
using AreWeDoomd.Application.Features.Users.Queries.GetUserPostsFeed;
using AreWeDoomd.Application.Features.Users.Queries.GetUserProfile;
using AreWeDoomd.Application.Features.Users.Queries.GetUserSuggestions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetMe(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var username = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username))
        {
            return Unauthorized();
        }

        var result = await mediator.Send(new GetUserProfileQuery(username, userId), cancellationToken);
        return this.ToActionResult(result, MapProfileDetail);
    }

    [HttpGet("{username}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetByUsername(
        string username,
        CancellationToken cancellationToken)
    {
        Guid? requesterId = TryGetCurrentUserId(out var id) ? id : null;
        var result = await mediator.Send(new GetUserProfileQuery(username, requesterId), cancellationToken);
        return this.ToActionResult(result, MapProfileDetail);
    }

    [HttpGet("discover")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserSummaryPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserSummaryPageResponse>> Discover(
        [FromQuery] int offset = 0,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        Guid? viewerId = TryGetCurrentUserId(out var id) ? id : null;
        var result = await mediator.Send(
            new GetUserSuggestionsQuery(viewerId, offset, pageSize), cancellationToken);
        return this.ToActionResult(result, MapSummaryPage);
    }

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
            new UpdateUserProfileCommand(userId, request.Username, request.Email, request.Bio),
            cancellationToken);

        return this.ToActionResult(result, MapProfileDetail);
    }

    [HttpPatch("me/profile-image")]
    [ProducesResponseType(typeof(UserAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserAccountResponse>> UpdateProfileImage(
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

        return this.ToActionResult(result, MapAccount);
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

    [HttpGet("{username}/posts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GlobalFeedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GlobalFeedResponse>> GetUserPostsFeed(
        string username,
        [FromQuery] DateTimeOffset? asOf,
        [FromQuery] int offset = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new GetUserPostsFeedQuery(username, asOf, offset, pageSize), cancellationToken);
        return this.ToActionResult(result, MapGlobalFeed);
    }

    [HttpGet("{username}/likes")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GlobalFeedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GlobalFeedResponse>> GetUserLikedPostsFeed(
        string username,
        [FromQuery] DateTimeOffset? asOf,
        [FromQuery] int offset = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new GetUserLikedPostsFeedQuery(username, asOf, offset, pageSize), cancellationToken);
        return this.ToActionResult(result, MapGlobalFeed);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out userId);
    }

    private static UserSummaryPageResponse MapSummaryPage(UserSummaryPageResult page)
        => new(page.Items.Select(i => new UserSummaryResponse(
            i.UserId, i.Username, i.UserType, i.ProfileImageUrl, i.Bio, i.IsFollowedByMe)).ToList(),
            page.HasMore);

    private static GlobalFeedResponse MapGlobalFeed(GlobalFeedResult result)
        => new(MapFeedPosts(result.Posts), result.AsOf, result.HasMore);

    private static IReadOnlyList<FeedPostResponse> MapFeedPosts(IReadOnlyList<FeedPostResult> results)
        => results.Select(r => new FeedPostResponse(
            r.Id,
            new PostAuthor(r.Author.UserId, r.Author.Username, r.Author.UserType, r.Author.ProfileImageUrl),
            r.Content, r.LikeCount, r.CommentCount,
            r.Comments.Select(MapFeedComment).ToList(),
            r.CreatedAt, r.UpdatedAt)).ToList();

    private static CommentResponse MapFeedComment(CommentResult c)
        => new(c.Id, c.PostId,
            new PostAuthor(c.Author.UserId, c.Author.Username, c.Author.UserType, c.Author.ProfileImageUrl),
            c.Content, c.LikeCount, c.CreatedAt, c.UpdatedAt);

    private static UserAccountResponse MapAccount(UserProfileResult result)
    {
        return new UserAccountResponse(
            result.UserId,
            result.Username,
            result.Email,
            result.UserType,
            result.ProfileImageUrl,
            result.Biography);
    }

    private static UserProfileResponse MapProfileDetail(UserProfileDetailResult r)
    {
        return new UserProfileResponse(
            r.UserId,
            r.Username,
            r.UserType,
            r.Bio,
            r.ProfileImageUrl,
            r.JoinedAt,
            new ProfileStatsResponse(
                r.Stats.PostCount, r.Stats.LikeCount, r.Stats.CommentCount,
                r.Stats.FollowerCount, r.Stats.FollowingCount),
            r.Badges.Select(b => new ProfileBadgeResponse(b.Code, b.Label, b.Description)).ToList(),
            r.IsFollowedByMe,
            r.IsMe);
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

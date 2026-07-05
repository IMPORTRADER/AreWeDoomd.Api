using AreWeDoomd.Api.Auth;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.PostScheduling;
using AreWeDoomd.Application.Features.PostScheduling.Commands.CancelScheduledPost;
using AreWeDoomd.Application.Features.PostScheduling.Commands.RetryScheduledPost;
using AreWeDoomd.Application.Features.PostScheduling.Commands.StartScheduleRun;
using AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateScheduledPost;
using AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateSchedulingSettings;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Application.Features.PostScheduling.Queries.GetScheduleRun;
using AreWeDoomd.Application.Features.PostScheduling.Queries.GetSchedulingSettings;
using AreWeDoomd.Application.Features.PostScheduling.Queries.ListScheduledPosts;
using AreWeDoomd.Application.Features.PostScheduling.Queries.ListScheduleRuns;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/admin/post-scheduling")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class PostSchedulingController(IMediator mediator) : ControllerBase
{
    [HttpPost("runs")]
    [ProducesResponseType(typeof(StartScheduleRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StartScheduleRunResponse>> StartRun(
        [FromBody] StartScheduleRunRequest request, CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await mediator.Send(
            new StartScheduleRunCommand(adminId, request.AiUserIds, request.OverwriteExisting),
            cancellationToken);
        return this.ToActionResult(result,
            r => new StartScheduleRunResponse(r.RunId, r.ItemCount), StatusCodes.Status202Accepted);
    }

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<ScheduleRunResponse>> GetRun(Guid runId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetScheduleRunQuery(runId), cancellationToken);
        return this.ToActionResult(result, MapRun);
    }

    [HttpGet("runs")]
    public async Task<ActionResult<List<ScheduleRunResponse>>> ListRuns(
        [FromQuery] DateOnly date, [FromQuery] int offset = 0, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListScheduleRunsQuery(date, offset, pageSize), cancellationToken);
        return this.ToActionResult(result, r => r.Items.Select(MapRun).ToList());
    }

    [HttpGet("posts")]
    public async Task<ActionResult<List<ScheduledPostResponse>>> ListPosts(
        [FromQuery] DateOnly date, [FromQuery] Guid? aiUserId, [FromQuery] int? status,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListScheduledPostsQuery(date, aiUserId, status), cancellationToken);
        return this.ToActionResult(result, r => r.Items.Select(MapPost).ToList());
    }

    [HttpPut("posts/{id:guid}")]
    public async Task<ActionResult<object>> UpdatePost(
        Guid id, [FromBody] UpdateScheduledPostRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateScheduledPostCommand(id, request.Content, request.ScheduledAtUtc), cancellationToken);
        return this.ToActionResult(result, _ => new object());
    }

    [HttpDelete("posts/{id:guid}")]
    public async Task<ActionResult<object>> CancelPost(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CancelScheduledPostCommand(id), cancellationToken);
        return this.ToActionResult(result, _ => new object());
    }

    [HttpPost("posts/{id:guid}/retry")]
    public async Task<ActionResult<object>> RetryPost(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RetryScheduledPostCommand(id), cancellationToken);
        return this.ToActionResult(result, _ => new object());
    }

    [HttpGet("settings")]
    public async Task<ActionResult<SchedulingSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSchedulingSettingsQuery(), cancellationToken);
        return this.ToActionResult(result, MapSettings);
    }

    [HttpPut("settings")]
    public async Task<ActionResult<SchedulingSettingsResponse>> UpdateSettings(
        [FromBody] UpdateSchedulingSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateSchedulingSettingsCommand(
            request.DesireThreshold, request.MaxPostsPerDay, request.PostLengthGuide,
            request.LatePolicy, request.LateGraceHours, request.Strategy), cancellationToken);
        return this.ToActionResult(result, MapSettings);
    }

    private static ScheduleRunResponse MapRun(ScheduleRunDetailResult r) =>
        new(r.Id, r.RunDate, r.ThresholdSnapshot, r.MaxPostsSnapshot, r.Status, r.CreatedAt, r.CompletedAt,
            r.Items.Select(i => new ScheduleRunItemResponse(
                i.Id, i.AiUserId, i.Username, i.ProfileImageUrl, i.Status, i.DesireScore, i.Reasoning,
                i.RequestedPostCount, i.DroppedPostCount, i.ModelUsed, i.ErrorDetail,
                i.Posts.Select(MapPost).ToList())).ToList());

    private static ScheduledPostResponse MapPost(ScheduledPostResult p) =>
        new(p.Id, p.ScheduleRunItemId, p.AiUserId, p.AiUsername, p.AiProfileImageUrl, p.Content,
            p.ScheduledAtUtc, p.Status, p.WasTimeAdjusted, p.ErrorMessage, p.PublishedAtUtc, p.PublishedPostId);

    private static SchedulingSettingsResponse MapSettings(SchedulingSettingsResult s) =>
        new(s.DesireThreshold, s.MaxPostsPerDay, s.PostLengthGuide, s.LatePolicy, s.LateGraceHours,
            s.Strategy, s.UpdatedAt);
}

using AreWeDoomd.Api.Auth;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Admin;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.AiManagement.Commands.CreateAiUser;
using AreWeDoomd.Application.Features.AiManagement.Commands.StartBulkCreateAiUsers;
using AreWeDoomd.Application.Features.AiManagement.Commands.UpdateAiPersonality;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentDecisions;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAiFleetStats;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetBulkCreateJob;
using AreWeDoomd.Application.Features.AiManagement.Queries.ListAiUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AiManagementController(IMediator mediator) : ControllerBase
{
    [HttpPost("ai-users")]
    [ProducesResponseType(typeof(AiUserDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AiUserDetailResponse>> CreateAiUser(
        [FromBody] CreateAiUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateAiUserCommand(request.Username, request.Email, request.Traits, request.TypingStyle, request.Summary),
            cancellationToken);
        return this.ToActionResult(result, MapAiUserDetail, StatusCodes.Status201Created);
    }

    [HttpGet("ai-users")]
    [ProducesResponseType(typeof(AiUserListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiUserListResponse>> ListAiUsers(
        [FromQuery] string? trait,
        [FromQuery] string? search,
        [FromQuery] int offset = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListAiUsersQuery(trait, search, offset, pageSize), cancellationToken);
        return this.ToActionResult(result, MapAiUserList);
    }

    [HttpGet("ai-users/{userId:guid}")]
    [ProducesResponseType(typeof(AiUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiUserDetailResponse>> GetAiUserDetail(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAiUserDetailQuery(userId), cancellationToken);
        return this.ToActionResult(result, MapAiUserDetail);
    }

    [HttpPut("ai-users/{userId:guid}/personality")]
    [ProducesResponseType(typeof(AiUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiUserDetailResponse>> UpdateAiPersonality(
        Guid userId,
        [FromBody] UpdateAiPersonalityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateAiPersonalityCommand(userId, request.Traits, request.TypingStyle, request.Summary),
            cancellationToken);
        return this.ToActionResult(result, MapAiUserDetail);
    }

    [HttpGet("decisions")]
    [ProducesResponseType(typeof(AgentDecisionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AgentDecisionsResponse>> GetAgentDecisions(
        [FromQuery] Guid? aiUserId,
        [FromQuery] string? action,
        [FromQuery] string? outcome,
        [FromQuery] DateOnly? fromUtc,
        [FromQuery] DateOnly? toUtc,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new GetAgentDecisionsQuery(aiUserId, action, outcome, fromUtc, toUtc, cursor, pageSize),
            cancellationToken);
        return this.ToActionResult(result, MapAgentDecisions);
    }

    [HttpGet("ai-stats")]
    [ProducesResponseType(typeof(AiFleetStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiFleetStatsResponse>> GetAiFleetStats(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAiFleetStatsQuery(), cancellationToken);
        return this.ToActionResult(result, MapAiFleetStats);
    }

    [HttpPost("ai-users/bulk")]
    [ProducesResponseType(typeof(StartBulkCreateResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StartBulkCreateResponse>> StartBulkCreate(
        [FromBody] StartBulkCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new StartBulkCreateAiUsersCommand(request.Count),
            cancellationToken);
        return this.ToActionResult(result, jobId => new StartBulkCreateResponse(jobId),
            StatusCodes.Status202Accepted);
    }

    [HttpGet("ai-users/bulk-jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(BulkCreateJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkCreateJobResponse>> GetBulkJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetBulkCreateJobQuery(jobId), cancellationToken);
        return this.ToActionResult(result, MapBulkCreateJob);
    }

    private static AiUserListResponse MapAiUserList(AiUserListResult r) =>
        new(r.Items.Select(MapAiUserItem).ToList(), r.TotalCount, r.HasMore);

    private static AiUserItemResponse MapAiUserItem(AiUserListItem i) =>
        new(i.Id, i.Username, i.ProfileImageUrl, i.CreatedAt, i.HasPersonality, i.Traits, i.TypingStyle, i.PersonaVersion);

    private static AiUserDetailResponse MapAiUserDetail(AiUserDetailResult r) =>
        new(r.Id, r.Username, r.Email, r.ProfileImageUrl, r.Biography, r.CreatedAt,
            r.HasPersonality, r.Traits, r.TypingStyle, r.Summary, r.PersonaVersion, r.PersonaUpdatedAt);

    private static AgentDecisionsResponse MapAgentDecisions(AgentDecisionsResult r) =>
        new(r.Items.Select(MapDecisionItem).ToList(), r.NextCursor, r.HasMore, r.LogAvailable);

    private static AgentDecisionItemResponse MapDecisionItem(DecisionLogRecord d) =>
        new(d.Ts, d.AiUserId, d.ActivityId, d.ActivityType, d.Outcome, d.Action, d.Reasoning,
            d.Content, d.PostId, d.CommentId, d.Priority, d.ErrorDetail, d.LlmAttempts,
            d.PersonaVersion, d.PersonaSource, d.SessionLogRef);

    private static AiFleetStatsResponse MapAiFleetStats(AiFleetStatsResult r) =>
        new(r.TotalAiUsers, r.WithPersonality, r.DecisionsToday, r.ExecutedToday,
            r.DroppedToday, r.FailedToday, r.ActionsLastHour, r.LogAvailable);

    private static BulkCreateJobResponse MapBulkCreateJob(BulkCreateJobSnapshot s) =>
        new(s.JobId, s.Status, s.Requested, s.Generated, s.Created,
            s.Failed.Select(f => new BulkCreateFailedEntryResponse(f.Username, f.Reason)).ToList(),
            s.CreatedUsers,
            s.StartedAt, s.FinishedAt, s.Rebuilt);
}

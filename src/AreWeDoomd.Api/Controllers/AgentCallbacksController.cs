using System.Security.Claims;
using AreWeDoomd.Api.Auth;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.AgentCallbacks;
using AreWeDoomd.Application.Features.PostScheduling.Commands.SubmitScheduleDecision;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/agent-callbacks")]
[Authorize(AuthenticationSchemes = AgentSecretAuthenticationDefaults.SchemeName, Roles = nameof(UserType.Ai))]
public sealed class AgentCallbacksController(IMediator mediator) : ControllerBase
{
    [HttpPost("schedule-decision")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<object>> SubmitDecision(
        [FromBody] SubmitScheduleDecisionRequest request, CancellationToken cancellationToken)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await mediator.Send(new SubmitScheduleDecisionCommand(
            request.RunItemId, callerId, request.DesireScore, request.Reasoning,
            request.RequestedPostCount, request.ModelUsed, request.ErrorDetail,
            request.Posts.Select(p => new SubmittedScheduledPost(p.Content, p.ScheduledAtUtc)).ToList()),
            cancellationToken);
        return this.ToActionResult(result, _ => new object());
    }
}

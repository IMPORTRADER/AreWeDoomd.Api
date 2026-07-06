using System.Security.Claims;
using AreWeDoomd.Api.Auth;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Admin;
using AreWeDoomd.Api.Contracts.Agents;
using AreWeDoomd.Application.Features.Agents.Queries.GetAgentPersona;
using AreWeDoomd.Application.Features.LlmSettings.Queries.GetLlmSettings;
using AreWeDoomd.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/agents")]
public sealed class AgentsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{userId:guid}/persona")]
    [Authorize]
    [ProducesResponseType(typeof(AgentPersonaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentPersonaResponse>> GetPersona(Guid userId, CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var callerId))
        {
            return Unauthorized();
        }

        if (callerId != userId)
        {
            return Forbid();
        }

        var result = await mediator.Send(new GetAgentPersonaQuery(userId), cancellationToken);
        return this.ToActionResult(result, MapPersona);
    }

    [HttpGet("llm-settings")]
    [Authorize(AuthenticationSchemes = AgentSecretAuthenticationDefaults.SchemeName, Roles = nameof(UserType.Ai))]
    [ProducesResponseType(typeof(LlmSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LlmSettingsResponse>> GetLlmSettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLlmSettingsQuery(), cancellationToken);
        return this.ToActionResult(result, s => new LlmSettingsResponse(
            s.Model, s.ScoringModel, s.ThinkingEnabled,
            s.ScoringTokensPerAccount, s.CompositionTokensPerPost,
            s.PersonaTokensPerPersona, s.ReplyMaxTokens, s.UpdatedAt));
    }

    private static AgentPersonaResponse MapPersona(AgentPersonaResult r) =>
        new(r.UserId, r.Traits, r.TypingStyle, r.Summary, r.Version);
}

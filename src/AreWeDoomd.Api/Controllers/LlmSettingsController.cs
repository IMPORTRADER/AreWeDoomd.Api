using AreWeDoomd.Api.Auth;
using AreWeDoomd.Api.Common.Results;
using AreWeDoomd.Api.Contracts.Admin;
using AreWeDoomd.Application.Features.LlmSettings.Commands.UpdateLlmSettings;
using AreWeDoomd.Application.Features.LlmSettings.Common;
using AreWeDoomd.Application.Features.LlmSettings.Queries.GetLlmSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/admin/llm-settings")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class LlmSettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(LlmSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LlmSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLlmSettingsQuery(), cancellationToken);
        return this.ToActionResult(result, Map);
    }

    [HttpPut]
    [ProducesResponseType(typeof(LlmSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LlmSettingsResponse>> Update(
        [FromBody] UpdateLlmSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateLlmSettingsCommand(
            request.Model, request.ScoringModel, request.ThinkingEnabled,
            request.ScoringTokensPerAccount, request.CompositionTokensPerPost,
            request.PersonaTokensPerPersona, request.ReplyMaxTokens), cancellationToken);
        return this.ToActionResult(result, Map);
    }

    private static LlmSettingsResponse Map(LlmSettingsResult s) => new(
        s.Model, s.ScoringModel, s.ThinkingEnabled,
        s.ScoringTokensPerAccount, s.CompositionTokensPerPost,
        s.PersonaTokensPerPersona, s.ReplyMaxTokens, s.UpdatedAt);
}

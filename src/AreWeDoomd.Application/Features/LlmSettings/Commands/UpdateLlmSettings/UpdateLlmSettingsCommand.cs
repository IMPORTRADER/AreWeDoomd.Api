using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.LlmSettings.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.LlmSettings.Commands.UpdateLlmSettings;

public sealed record UpdateLlmSettingsCommand(
    string Model,
    string ScoringModel,
    bool ThinkingEnabled,
    int ScoringTokensPerAccount,
    int CompositionTokensPerPost,
    int PersonaTokensPerPersona,
    int ReplyMaxTokens) : IRequest<Result<LlmSettingsResult>>;

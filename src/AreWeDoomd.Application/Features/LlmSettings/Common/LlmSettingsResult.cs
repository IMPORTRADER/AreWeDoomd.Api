namespace AreWeDoomd.Application.Features.LlmSettings.Common;

public sealed record LlmSettingsResult(
    string Model,
    string ScoringModel,
    bool ThinkingEnabled,
    int ScoringTokensPerAccount,
    int CompositionTokensPerPost,
    int PersonaTokensPerPersona,
    int ReplyMaxTokens,
    DateTimeOffset UpdatedAt);

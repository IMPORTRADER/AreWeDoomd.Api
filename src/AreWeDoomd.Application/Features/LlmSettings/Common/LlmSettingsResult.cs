namespace AreWeDoomd.Application.Features.LlmSettings.Common;

public sealed record LlmSettingsResult(
    string Model,
    string ScoringModel,
    bool ThinkingEnabled,
    string Provider,
    int ScoringTokensPerAccount,
    int CompositionTokensPerPost,
    int ReplyMaxTokens,
    DateTimeOffset UpdatedAt);

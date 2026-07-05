namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record LlmSettingsResponse(
    string Model,
    string ScoringModel,
    bool ThinkingEnabled,
    int ScoringTokensPerAccount,
    int CompositionTokensPerPost,
    int PersonaTokensPerPersona,
    int ReplyMaxTokens,
    DateTimeOffset UpdatedAt);

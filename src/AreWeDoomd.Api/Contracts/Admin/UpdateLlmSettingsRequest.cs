namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record UpdateLlmSettingsRequest(
    string Model,
    string ScoringModel,
    bool ThinkingEnabled,
    string? Provider,
    int ScoringTokensPerAccount,
    int CompositionTokensPerPost,
    int ReplyMaxTokens);

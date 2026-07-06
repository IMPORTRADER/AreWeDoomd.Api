namespace AreWeDoomd.AgentService.Prompting;

public sealed record LlmRuntimeSettings(
    string Model,
    string ScoringModel,
    bool ThinkingEnabled,
    int ScoringTokensPerAccount,
    int CompositionTokensPerPost,
    int PersonaTokensPerPersona,
    int ReplyMaxTokens);

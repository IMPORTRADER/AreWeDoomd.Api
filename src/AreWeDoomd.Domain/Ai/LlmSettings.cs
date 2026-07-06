namespace AreWeDoomd.Domain.Ai;

public sealed class LlmSettings
{
    public static readonly Guid SingletonId = new("7b5a1a6e-0000-4000-8000-000000000002");

    public const string DefaultModel = "openai/gpt-oss-120b:free";
    public const int MinTokenBudget = 128;
    public const int MaxTokenBudget = 8192;

    public Guid Id { get; private set; }
    public string Model { get; private set; } = string.Empty;
    /// <summary>Puanlama için ayrı model; boş = Model kullanılır.</summary>
    public string ScoringModel { get; private set; } = string.Empty;
    public bool ThinkingEnabled { get; private set; }
    public int ScoringTokensPerAccount { get; private set; }
    public int CompositionTokensPerPost { get; private set; }
    public int PersonaTokensPerPersona { get; private set; }
    public int ReplyMaxTokens { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private LlmSettings() { } // EF Core

    public static LlmSettings CreateDefault(DateTimeOffset now)
    {
        var settings = new LlmSettings { Id = SingletonId };
        settings.Update(
            model: DefaultModel, scoringModel: string.Empty, thinkingEnabled: false,
            scoringTokensPerAccount: 512, compositionTokensPerPost: 800,
            personaTokensPerPersona: 700, replyMaxTokens: 1024, now: now);
        return settings;
    }

    public void Update(
        string model, string scoringModel, bool thinkingEnabled,
        int scoringTokensPerAccount, int compositionTokensPerPost,
        int personaTokensPerPersona, int replyMaxTokens, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model must not be blank.", nameof(model));
        }
        ValidateBudget(scoringTokensPerAccount, nameof(scoringTokensPerAccount));
        ValidateBudget(compositionTokensPerPost, nameof(compositionTokensPerPost));
        ValidateBudget(personaTokensPerPersona, nameof(personaTokensPerPersona));
        ValidateBudget(replyMaxTokens, nameof(replyMaxTokens));

        Model = model.Trim();
        ScoringModel = scoringModel?.Trim() ?? string.Empty;
        ThinkingEnabled = thinkingEnabled;
        ScoringTokensPerAccount = scoringTokensPerAccount;
        CompositionTokensPerPost = compositionTokensPerPost;
        PersonaTokensPerPersona = personaTokensPerPersona;
        ReplyMaxTokens = replyMaxTokens;
        UpdatedAt = now;
    }

    private static void ValidateBudget(int value, string paramName)
    {
        if (value is < MinTokenBudget or > MaxTokenBudget)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}

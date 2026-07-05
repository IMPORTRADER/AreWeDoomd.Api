using AreWeDoomd.AgentService.Processing;

namespace AreWeDoomd.AgentService.Prompting;

public sealed class PromptFileSet
{
    private readonly IReadOnlyDictionary<EffectivePriority, string> _priorityInstructions;

    public PromptFileSet(string? promptsRoot = null)
    {
        string root = promptsRoot ?? Path.Combine(AppContext.BaseDirectory, "Prompts");

        Base = File.ReadAllText(Path.Combine(root, "00-base.md"));
        CommentCreatedTask = File.ReadAllText(Path.Combine(root, "20-tasks", "comment-created.md"));
        DailyPostScoreTask = File.ReadAllText(Path.Combine(root, "20-tasks", "daily-post-score.md"));
        DailyPostComposeTask = File.ReadAllText(Path.Combine(root, "20-tasks", "daily-post-compose.md"));
        Guardrails = File.ReadAllText(Path.Combine(root, "90-guardrails.md"));

        string instructionsRoot = Path.Combine(root, "30-priority-instructions");
        _priorityInstructions = new Dictionary<EffectivePriority, string>
        {
            [EffectivePriority.High] = File.ReadAllText(Path.Combine(instructionsRoot, "high.md")),
            [EffectivePriority.Normal] = File.ReadAllText(Path.Combine(instructionsRoot, "normal.md")),
            [EffectivePriority.Low] = File.ReadAllText(Path.Combine(instructionsRoot, "low.md")),
            [EffectivePriority.LowClosing] = File.ReadAllText(Path.Combine(instructionsRoot, "closing.md"))
        };
    }

    public string Base { get; }

    public string CommentCreatedTask { get; }

    public string DailyPostScoreTask { get; }

    public string DailyPostComposeTask { get; }

    public string Guardrails { get; }

    public string PriorityInstruction(EffectivePriority priority)
    {
        return _priorityInstructions[priority];
    }
}

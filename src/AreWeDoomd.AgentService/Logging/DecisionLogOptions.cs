namespace AreWeDoomd.AgentService.Logging;

public sealed class DecisionLogOptions
{
    public const string SectionName = "DecisionLog";

    /// <summary>Directory for decisions-{yyyy-MM-dd}.jsonl files. Relative paths resolve against the process working directory (same convention as the ai-sessions logs).</summary>
    public string RootPath { get; set; } = Path.Combine("logs", "agent-decisions");

    /// <summary>Daily files older than this are deleted by the writer.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>Bounded capacity of the in-memory entry channel.</summary>
    public int QueueCapacity { get; set; } = 1000;
}

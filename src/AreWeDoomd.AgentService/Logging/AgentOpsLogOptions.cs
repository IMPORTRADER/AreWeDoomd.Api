namespace AreWeDoomd.AgentService.Logging;

public sealed class AgentOpsLogOptions
{
    public const string SectionName = "AgentOpsLog";

    /// <summary>Directory for agent-logs-{process}-{yyyy-MM-dd}.jsonl files. Relative paths resolve against the process working directory (same convention as the decision logs).</summary>
    public string RootPath { get; set; } = Path.Combine("logs", "agent-ops");

    /// <summary>Daily files older than this are deleted by the writer.</summary>
    public int RetentionDays { get; set; } = 14;

    /// <summary>Bounded capacity of the in-memory entry channel.</summary>
    public int QueueCapacity { get; set; } = 2000;
}

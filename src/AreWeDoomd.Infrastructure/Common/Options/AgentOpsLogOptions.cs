namespace AreWeDoomd.Infrastructure.Common.Options;

public sealed class AgentOpsLogOptions
{
    public const string SectionName = "AgentOpsLog";

    /// <summary>Directory shared with the AgentService's ops log (same volume in docker).</summary>
    public string RootPath { get; set; } = Path.Combine("logs", "agent-ops");

    /// <summary>Daily files older than this are deleted by the API-side writer.</summary>
    public int RetentionDays { get; set; } = 14;

    /// <summary>Bounded capacity of the in-memory entry channel.</summary>
    public int QueueCapacity { get; set; } = 2000;
}

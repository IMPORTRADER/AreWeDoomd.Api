namespace AreWeDoomd.Application.Common.Models;

/// <summary>Wire values must match the AgentService's snake_case enum serialization.</summary>
public static class AgentOpsLogSources
{
    public const string Pipeline = "pipeline";
    public const string LlmProvider = "llm_provider";
    public const string Actions = "actions";
    public const string Scheduling = "scheduling";
    public const string Admin = "admin";
}

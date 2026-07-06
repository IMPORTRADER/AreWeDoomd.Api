namespace AreWeDoomd.Infrastructure.Common.Options;

public sealed class DecisionLogOptions
{
    public const string SectionName = "DecisionLog";

    public string RootPath { get; set; } = Path.Combine("logs", "agent-decisions");
}

using AreWeDoomd.AgentService.Logging;

namespace AreWeDoomd.UnitTests.AgentService;

public sealed class FakeAgentOpsLogWriter : IAgentOpsLogWriter
{
    public List<AgentOpsLogEntry> Entries { get; } = [];

    public bool TryLog(AgentOpsLogEntry entry)
    {
        Entries.Add(entry);
        return true;
    }
}

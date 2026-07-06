using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.UnitTests.Application;

public sealed class StubAgentOpsLogger : IAgentOpsLogger
{
    public List<AgentOpsLogRecord> Entries { get; } = [];

    public bool TryLog(AgentOpsLogRecord entry)
    {
        Entries.Add(entry);
        return true;
    }
}

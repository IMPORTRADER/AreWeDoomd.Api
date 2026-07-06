using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

/// <summary>API-side ops log writer. Never blocks, never throws — a logging
/// failure must never fail the operation being logged.</summary>
public interface IAgentOpsLogger
{
    bool TryLog(AgentOpsLogRecord entry);
}

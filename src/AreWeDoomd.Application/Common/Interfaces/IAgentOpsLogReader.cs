using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAgentOpsLogReader
{
    Task<AgentOpsLogPage> ReadAsync(AgentOpsLogFilter filter, string? cursor, int pageSize, CancellationToken ct);
}

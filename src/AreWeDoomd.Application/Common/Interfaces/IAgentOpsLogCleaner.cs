namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAgentOpsLogCleaner
{
    /// <summary>Deletes all ops log files. Returns the number of files deleted.</summary>
    Task<int> ClearAsync(CancellationToken ct);
}

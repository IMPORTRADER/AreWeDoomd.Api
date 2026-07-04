namespace AreWeDoomd.AgentService.Logging;

public interface IDecisionLogWriter
{
    /// <summary>Never blocks, never throws. False = entry was rejected (channel full/closed) — already counted and logged.</summary>
    bool TryLog(DecisionLogEntry entry);
}

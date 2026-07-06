using AreWeDoomd.ChatProviders;

namespace AreWeDoomd.AgentService.Ai;

public interface IAiSessionLogger
{
    /// <summary>Writes the session file and returns its ref relative to logs/ai-sessions, e.g. "2026-07-04/act-1_attempt1_123105.txt". Null if the file write failed (console log still emitted).</summary>
    string? Log(string activityId, int attempt, ChatRequest request, ChatResult result);
}

namespace AreWeDoomd.AgentService.Ai;

public interface IAiSessionLogger
{
    void Log(string activityId, int attempt, ChatRequest request, ChatResult result);
}

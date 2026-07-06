namespace AreWeDoomd.AgentService.Actions;

public interface IScheduleDecisionCallbackClient
{
    Task<bool> SubmitAsync(
        Guid actingAiUserId,
        ScheduleDecisionCallbackClient.CallbackPayload payload,
        CancellationToken ct);
}

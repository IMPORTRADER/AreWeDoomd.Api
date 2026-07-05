namespace AreWeDoomd.Api.Contracts.AgentCallbacks;

public sealed record SubmitScheduleDecisionRequest(
    Guid RunItemId,
    int DesireScore,
    string? Reasoning,
    int RequestedPostCount,
    string? ModelUsed,
    string? ErrorDetail,
    List<SubmittedPostRequest> Posts);

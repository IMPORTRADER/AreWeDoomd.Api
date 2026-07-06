namespace AreWeDoomd.Api.Contracts.AgentCallbacks;

public sealed record SubmittedPostRequest(string Content, DateTimeOffset ScheduledAtUtc);

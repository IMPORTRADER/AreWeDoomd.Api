namespace AreWeDoomd.Api.Contracts.PostScheduling;

public sealed record UpdateScheduledPostRequest(string Content, DateTimeOffset ScheduledAtUtc);

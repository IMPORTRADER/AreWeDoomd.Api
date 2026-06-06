namespace AreWeDoomd.AgentService.Ai;

public sealed record ChatError(string Message, int? StatusCode, string Provider);

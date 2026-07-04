namespace AreWeDoomd.ChatProviders;

public sealed record ChatError(string Message, int? StatusCode, string Provider);

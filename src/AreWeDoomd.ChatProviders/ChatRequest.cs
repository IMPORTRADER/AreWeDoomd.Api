namespace AreWeDoomd.ChatProviders;

public sealed record ChatRequest(
    string Model,
    IReadOnlyList<ChatMessage> Messages,
    string? System = null,
    int? MaxTokens = null,
    double? Temperature = null,
    string? JsonResponseSchema = null);

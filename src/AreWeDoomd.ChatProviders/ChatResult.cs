namespace AreWeDoomd.ChatProviders;

public sealed record ChatResult
{
    public bool IsSuccess { get; init; }
    public string? Text { get; init; }
    public TokenUsage? Usage { get; init; }
    public FinishReason? Finish { get; init; }
    public ChatError? Error { get; init; }

    public static ChatResult Ok(string text, TokenUsage usage, FinishReason finish) =>
        new() { IsSuccess = true, Text = text, Usage = usage, Finish = finish };

    public static ChatResult Fail(ChatError error) =>
        new() { IsSuccess = false, Error = error };
}

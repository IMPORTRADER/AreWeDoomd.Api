using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.Gemini.Wire;

/// <summary>
/// Error envelope returned by the Gemini API on non-2xx responses, e.g.
/// <c>{"error":{"code":429,"message":"...","status":"RESOURCE_EXHAUSTED"}}</c>.
/// </summary>
internal sealed record GeminiErrorResponse(
    [property: JsonPropertyName("error")] GeminiError? Error);

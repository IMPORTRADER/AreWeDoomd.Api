using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.Gemini.Wire;

/// <summary>
/// A content block used in three places: request <c>contents</c> turns (role
/// "user"), the <c>systemInstruction</c> (role omitted), and the response
/// candidate content (role "model"). Null role is omitted during serialization.
/// </summary>
internal sealed record GeminiContent(
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

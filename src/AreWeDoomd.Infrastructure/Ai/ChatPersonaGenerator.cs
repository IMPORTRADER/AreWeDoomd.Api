using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.ChatProviders;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.Infrastructure.Ai;

/// <summary>
/// Generates batches of AI user personas by prompting an <see cref="IChatProvider"/>
/// with a structured-output JSON schema.
///
/// <para>Design: <c>isConfigured</c> is resolved at DI registration time by the
/// factory lambda (which reads the configured provider's ApiKey from
/// IConfiguration) and injected as a plain bool. This keeps the class
/// independent of IConfiguration and trivially testable with a direct ctor call.</para>
/// </summary>
public sealed class ChatPersonaGenerator : IPersonaGenerator
{
    private static readonly Regex ValidUsernameChars = new(@"[^a-z0-9_]", RegexOptions.Compiled);

    private const int UsernameMinLength = 3;
    private const int UsernameMaxLength = 24;
    private const int TraitMinLength = 2;
    private const int TraitMaxLength = 60;
    private const int TraitMaxCount = 10;
    private const int TypingStyleMaxLength = 500;
    private const int SummaryMaxLength = 1000;

    private readonly IChatProvider _provider;
    private readonly PersonaGenerationOptions _options;
    private readonly ILogger<ChatPersonaGenerator> _logger;

    public bool IsConfigured { get; }

    public ChatPersonaGenerator(
        IChatProvider provider,
        PersonaGenerationOptions options,
        ILogger<ChatPersonaGenerator> logger,
        bool isConfigured)
    {
        _provider = provider;
        _options = options;
        _logger = logger;
        IsConfigured = isConfigured;
    }

    public async Task<Result<IReadOnlyList<GeneratedPersona>>> GenerateBatchAsync(
        int count,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return Result<IReadOnlyList<GeneratedPersona>>.Failure(
                "persona.generator_unconfigured",
                "The persona generator is not configured: the selected provider has no API key.");
        }

        var request = new ChatRequest(
            Model: _options.Model,
            Messages: [new ChatMessage(BuildUserPrompt(count))],
            System: BuildSystemPrompt(),
            Temperature: 1.0,
            JsonResponseSchema: PersonaBatchSchema.Json);

        var chatResult = await _provider.CompleteAsync(request, ct);

        if (!chatResult.IsSuccess)
        {
            _logger.LogWarning(
                "Persona generation provider call failed: {Message}",
                chatResult.Error!.Message);

            return Result<IReadOnlyList<GeneratedPersona>>.Failure(
                "persona.generation_failed",
                chatResult.Error.Message);
        }

        return ParseAndSanitize(chatResult.Text ?? string.Empty);
    }

    // ── prompt construction ───────────────────────────────────────────────────

    private static string BuildSystemPrompt() =>
        "You are a creative writer inventing fictional AI social-media user personas. " +
        "Return ONLY the JSON object described by the schema — no extra text. " +
        "Make every persona distinct: vary political lean, tone, interests, and age group.";

    private static string BuildUserPrompt(int count) =>
        $"Generate exactly {count} DIVERSE social-media AI user personas. " +
        "Rules: usernames must be lowercase alphanumeric/underscore, 3–24 chars, memorable; " +
        "traits are 2–6 short free-form descriptors (e.g. \"sarcastic\", \"activist\"); " +
        "typingStyle is one instruction sentence; " +
        "summary is one paragraph about the persona's personality and posting behaviour.";

    // ── parse + sanitize ──────────────────────────────────────────────────────

    private Result<IReadOnlyList<GeneratedPersona>> ParseAndSanitize(string text)
    {
        PersonaBatch? batch;

        try
        {
            batch = JsonSerializer.Deserialize<PersonaBatch>(
                text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Persona JSON parse failed: {Message}", ex.Message);
            return Result<IReadOnlyList<GeneratedPersona>>.Failure(
                "persona.generation_failed",
                $"Failed to parse persona JSON: {ex.Message}");
        }

        if (batch?.Personas is null)
        {
            return Result<IReadOnlyList<GeneratedPersona>>.Failure(
                "persona.generation_failed",
                "Provider returned JSON that does not contain a 'personas' array.");
        }

        var results = new List<GeneratedPersona>(batch.Personas.Count);

        foreach (var raw in batch.Personas)
        {
            var persona = Sanitize(raw);
            if (persona is not null)
            {
                results.Add(persona);
            }
        }

        return Result<IReadOnlyList<GeneratedPersona>>.Success(results);
    }

    private static GeneratedPersona? Sanitize(RawPersona raw)
    {
        // ── username ─────────────────────────────────────────────────────────
        var username = ValidUsernameChars.Replace(
            (raw.Username ?? string.Empty).ToLowerInvariant(),
            string.Empty);

        if (username.Length > UsernameMaxLength)
        {
            username = username[..UsernameMaxLength];
        }

        if (username.Length < UsernameMinLength)
        {
            return null; // unsalvageable
        }

        // ── traits ───────────────────────────────────────────────────────────
        var traits = (raw.Traits ?? [])
            .Select(t => t?.Trim() ?? string.Empty)
            .Where(t => t.Length >= TraitMinLength && t.Length <= TraitMaxLength)
            .Take(TraitMaxCount)
            .ToList();

        if (traits.Count == 0)
        {
            return null; // no valid traits
        }

        // ── typingStyle ───────────────────────────────────────────────────────
        var typingStyle = Truncate(raw.TypingStyle?.Trim(), TypingStyleMaxLength);
        if (string.IsNullOrEmpty(typingStyle))
        {
            return null;
        }

        // ── summary ───────────────────────────────────────────────────────────
        var summary = Truncate(raw.Summary?.Trim(), SummaryMaxLength);
        if (string.IsNullOrEmpty(summary))
        {
            return null;
        }

        return new GeneratedPersona(username, traits, typingStyle, summary);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null ? null :
        value.Length <= maxLength ? value :
        value[..maxLength];

    // ── wire DTOs (private — never escape this class) ─────────────────────────

    private sealed class PersonaBatch
    {
        [JsonPropertyName("personas")]
        public List<RawPersona> Personas { get; set; } = [];
    }

    private sealed class RawPersona
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("traits")]
        public List<string?>? Traits { get; set; }

        [JsonPropertyName("typingStyle")]
        public string? TypingStyle { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}

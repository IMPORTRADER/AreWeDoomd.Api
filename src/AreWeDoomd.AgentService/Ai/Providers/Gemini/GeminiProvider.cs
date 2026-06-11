using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AreWeDoomd.AgentService.Ai.Providers.Gemini.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Ai.Providers.Gemini;

/// <summary>
/// Hand-rolled adapter for the Google Gemini <c>generateContent</c> API. Cloned
/// from <c>AnthropicProvider</c>: it owns its wire DTOs, never lets
/// provider-specific types escape, and obeys the same hard rules — callers only
/// ever see <see cref="ChatResult"/>.
/// </summary>
public sealed class GeminiProvider : IChatProvider
{
    public const string ProviderName = "gemini";
    public const string HttpClientName = "gemini";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiProviderOptions _options;
    private readonly ILogger<GeminiProvider> _logger;

    public GeminiProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiProviderOptions> options,
        ILogger<GeminiProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    private static readonly HashSet<int> RetryableStatusCodes = [429, 502, 503, 504];

    public string Name => ProviderName;

    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        int maxAttempts = _options.MaxRetries + 1;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            bool shouldRetry = false;
            int statusCode = 0;

            try
            {
                using var httpRequest = BuildHttpRequest(request);
                HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

                using HttpResponseMessage response = await client.SendAsync(httpRequest, ct);
                statusCode = (int)response.StatusCode;
                string responseBody = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    if (RetryableStatusCodes.Contains(statusCode) && attempt < maxAttempts)
                        shouldRetry = true;
                    else
                        return ChatResult.Fail(NormalizeError(responseBody, statusCode));
                }
                else
                {
                    return BuildResult(responseBody, statusCode);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
            {
                _logger.LogWarning(ex, "Transport failure calling the Gemini generateContent API");
                return ChatResult.Fail(new ChatError(ex.Message, StatusCode: null, ProviderName));
            }

            if (shouldRetry)
            {
                int delayMs = _options.RetryBaseDelayMs * (int)Math.Pow(2, attempt - 1);
                _logger.LogWarning(
                    "Gemini returned {StatusCode} (transient) on attempt {Attempt}/{Max}; retrying in {DelayMs}ms",
                    statusCode, attempt, maxAttempts, delayMs);
                await Task.Delay(delayMs, ct);
            }
        }

        return ChatResult.Fail(new ChatError(
            $"Gemini request failed after {_options.MaxRetries} retries.",
            null,
            ProviderName));
    }

    private HttpRequestMessage BuildHttpRequest(ChatRequest request)
    {
        string model = string.IsNullOrWhiteSpace(request.Model)
            ? _options.DefaultModel
            : request.Model;

        // Gemini does not require maxOutputTokens, but we send the configured
        // default when the caller did not specify one for consistent behavior.
        // This fallback lives here, not in the domain ChatRequest.
        int maxTokens = request.MaxTokens ?? _options.DefaultMaxTokens;

        JsonElement? responseSchema = null;
        string? responseMimeType = null;
        if (!string.IsNullOrWhiteSpace(request.JsonResponseSchema))
        {
            using JsonDocument schemaDocument = JsonDocument.Parse(request.JsonResponseSchema);
            responseSchema = schemaDocument.RootElement.Clone();
            responseMimeType = "application/json";
        }

        var payload = new GeminiGenerateContentRequest
        {
            Contents = request.Messages
                .Select(message => new GeminiContent(
                    "user",
                    [new GeminiPart(message.Content)]))
                .ToList(),
            SystemInstruction = string.IsNullOrWhiteSpace(request.System)
                ? null
                : new GeminiContent(null, [new GeminiPart(request.System)]),
            GenerationConfig = new GeminiGenerationConfig
            {
                MaxOutputTokens = maxTokens,
                Temperature = request.Temperature,
                ResponseMimeType = responseMimeType,
                ResponseSchema = responseSchema
            }
        };

        string json = JsonSerializer.Serialize(payload, SerializerOptions);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri(model))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        // Header auth keeps the API key out of the request URI (and therefore
        // out of logs/exception messages); the ?key= query param is equivalent.
        httpRequest.Headers.Add("x-goog-api-key", _options.ApiKey);

        return httpRequest;
    }

    private Uri BuildRequestUri(string model)
    {
        string baseUrl = _options.BaseUrl.TrimEnd('/');
        // The path already contains "models/", so a bare model id is expected.
        string modelId = model.StartsWith("models/", StringComparison.Ordinal)
            ? model["models/".Length..]
            : model;
        return new Uri($"{baseUrl}/{_options.ApiVersion}/models/{modelId}:generateContent");
    }

    private ChatResult BuildResult(string responseBody, int statusCode)
    {
        GeminiGenerateContentResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseBody, SerializerOptions);
        }
        catch (JsonException ex)
        {
            // 200 OK but the body is not something we can read — no usable answer.
            _logger.LogWarning(ex, "Could not parse a successful Gemini response body");
            return ChatResult.Fail(new ChatError(
                "Gemini returned a response that could not be parsed.",
                statusCode,
                ProviderName));
        }

        GeminiCandidate? candidate = parsed?.Candidates is { Count: > 0 } candidates
            ? candidates[0]
            : null;

        FinishReason finish = MapFinishReason(candidate?.FinishReason);
        string text = ExtractText(candidate);

        // Rule #4: a content-filter / safety stop (or any empty body) is a Fail,
        // not an Ok carrying empty text.
        if (string.IsNullOrWhiteSpace(text))
        {
            string message = finish == FinishReason.ContentFilter
                ? "Gemini stopped on a safety / content filter and returned no text."
                : "Gemini returned no usable text content.";
            return ChatResult.Fail(new ChatError(message, statusCode, ProviderName));
        }

        var usage = parsed?.UsageMetadata is { } u
            ? new TokenUsage(u.PromptTokenCount, u.CandidatesTokenCount)
            : new TokenUsage(0, 0);

        return ChatResult.Ok(text, usage, finish);
    }

    private ChatError NormalizeError(string responseBody, int statusCode)
    {
        string? message = null;
        try
        {
            GeminiErrorResponse? error =
                JsonSerializer.Deserialize<GeminiErrorResponse>(responseBody, SerializerOptions);
            message = error?.Error?.Message;
        }
        catch (JsonException)
        {
            // Non-JSON error body — fall through to the raw-body fallback below.
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = string.IsNullOrWhiteSpace(responseBody)
                ? $"Gemini request failed with status {statusCode}."
                : responseBody.Trim();
        }

        return new ChatError(message, statusCode, ProviderName);
    }

    private static string ExtractText(GeminiCandidate? candidate)
    {
        if (candidate?.Content?.Parts is null)
        {
            return string.Empty;
        }

        return string.Concat(candidate.Content.Parts.Select(part => part.Text));
    }

    private static FinishReason MapFinishReason(string? finishReason) => finishReason switch
    {
        "STOP" => FinishReason.Stop,
        "MAX_TOKENS" => FinishReason.MaxTokens,
        "SAFETY" => FinishReason.ContentFilter,
        "RECITATION" => FinishReason.ContentFilter,
        "PROHIBITED_CONTENT" => FinishReason.ContentFilter,
        "BLOCKLIST" => FinishReason.ContentFilter,
        "SPII" => FinishReason.ContentFilter,
        _ => FinishReason.Other
    };
}

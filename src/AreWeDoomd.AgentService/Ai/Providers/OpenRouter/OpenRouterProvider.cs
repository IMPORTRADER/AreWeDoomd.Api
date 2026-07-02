using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter;

/// <summary>
/// Hand-rolled adapter for OpenRouter's OpenAI-compatible chat completions API.
/// Cloned from <c>GeminiProvider</c>: it owns its wire DTOs, never lets
/// provider-specific types escape, and obeys the same hard rules — callers only
/// ever see <see cref="ChatResult"/>.
/// </summary>
public sealed class OpenRouterProvider : IChatProvider
{
    public const string ProviderName = "openrouter";
    public const string HttpClientName = "openrouter";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<int> RetryableStatusCodes = [429, 502, 503, 504];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenRouterProviderOptions _options;
    private readonly ILogger<OpenRouterProvider> _logger;

    public OpenRouterProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenRouterProviderOptions> options,
        ILogger<OpenRouterProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

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
                    {
                        shouldRetry = true;
                    }
                    else
                    {
                        return ChatResult.Fail(NormalizeError(responseBody, statusCode));
                    }
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
                _logger.LogWarning(ex, "Transport failure calling the OpenRouter chat completions API");
                return ChatResult.Fail(new ChatError(ex.Message, StatusCode: null, ProviderName));
            }

            if (shouldRetry)
            {
                int delayMs = _options.RetryBaseDelayMs * (int)Math.Pow(2, attempt - 1);
                _logger.LogWarning(
                    "OpenRouter returned {StatusCode} (transient) on attempt {Attempt}/{Max}; retrying in {DelayMs}ms",
                    statusCode, attempt, maxAttempts, delayMs);
                await Task.Delay(delayMs, ct);
            }
        }

        return ChatResult.Fail(new ChatError(
            $"OpenRouter request failed after {_options.MaxRetries} retries.",
            null,
            ProviderName));
    }

    private HttpRequestMessage BuildHttpRequest(ChatRequest request)
    {
        string model = string.IsNullOrWhiteSpace(request.Model)
            ? _options.DefaultModel
            : request.Model;

        int maxTokens = request.MaxTokens ?? _options.DefaultMaxTokens;

        var messages = new List<OpenRouterMessage>();
        if (!string.IsNullOrWhiteSpace(request.System))
        {
            messages.Add(new OpenRouterMessage("system", request.System));
        }

        messages.AddRange(request.Messages.Select(message => new OpenRouterMessage("user", message.Content)));

        OpenRouterResponseFormat? responseFormat = null;
        if (!string.IsNullOrWhiteSpace(request.JsonResponseSchema))
        {
            using JsonDocument schemaDocument = JsonDocument.Parse(request.JsonResponseSchema);
            responseFormat = new OpenRouterResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new OpenRouterJsonSchema
                {
                    Name = "response",
                    Schema = schemaDocument.RootElement.Clone()
                }
            };
        }

        var payload = new OpenRouterChatCompletionRequest
        {
            Model = model,
            Messages = messages,
            MaxTokens = maxTokens,
            Temperature = request.Temperature,
            ResponseFormat = responseFormat
        };

        string json = JsonSerializer.Serialize(payload, SerializerOptions);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri())
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
        if (!string.IsNullOrWhiteSpace(_options.SiteUrl))
        {
            httpRequest.Headers.Add("HTTP-Referer", _options.SiteUrl);
        }

        if (!string.IsNullOrWhiteSpace(_options.SiteName))
        {
            httpRequest.Headers.Add("X-Title", _options.SiteName);
        }

        return httpRequest;
    }

    private Uri BuildRequestUri()
    {
        string baseUrl = _options.BaseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}/chat/completions");
    }

    private ChatResult BuildResult(string responseBody, int statusCode)
    {
        OpenRouterChatCompletionResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenRouterChatCompletionResponse>(responseBody, SerializerOptions);
        }
        catch (JsonException ex)
        {
            // 200 OK but the body is not something we can read — no usable answer.
            _logger.LogWarning(ex, "Could not parse a successful OpenRouter response body");
            return ChatResult.Fail(new ChatError(
                "OpenRouter returned a response that could not be parsed.",
                statusCode,
                ProviderName));
        }

        OpenRouterChoice? choice = parsed?.Choices is { Count: > 0 } choices
            ? choices[0]
            : null;

        FinishReason finish = MapFinishReason(choice?.FinishReason);
        string text = choice?.Message?.Content ?? string.Empty;

        // Rule #4: a content-filter stop (or any empty body) is a Fail, not an
        // Ok carrying empty text.
        if (string.IsNullOrWhiteSpace(text))
        {
            string message = finish == FinishReason.ContentFilter
                ? "OpenRouter stopped on a content filter and returned no text."
                : "OpenRouter returned no usable text content.";
            return ChatResult.Fail(new ChatError(message, statusCode, ProviderName));
        }

        var usage = parsed?.Usage is { } u
            ? new TokenUsage(u.PromptTokens, u.CompletionTokens)
            : new TokenUsage(0, 0);

        return ChatResult.Ok(text, usage, finish);
    }

    private ChatError NormalizeError(string responseBody, int statusCode)
    {
        string? message = null;
        try
        {
            OpenRouterErrorResponse? error =
                JsonSerializer.Deserialize<OpenRouterErrorResponse>(responseBody, SerializerOptions);
            message = error?.Error?.Message;
        }
        catch (JsonException)
        {
            // Non-JSON error body — fall through to the raw-body fallback below.
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = string.IsNullOrWhiteSpace(responseBody)
                ? $"OpenRouter request failed with status {statusCode}."
                : responseBody.Trim();
        }

        return new ChatError(message, statusCode, ProviderName);
    }

    private static FinishReason MapFinishReason(string? finishReason) => finishReason switch
    {
        "stop" => FinishReason.Stop,
        "length" => FinishReason.MaxTokens,
        "content_filter" => FinishReason.ContentFilter,
        _ => FinishReason.Other
    };
}

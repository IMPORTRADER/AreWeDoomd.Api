using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AreWeDoomd.AgentService.Ai.Providers.Anthropic.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic;

/// <summary>
/// Hand-rolled adapter for the Anthropic Messages API
/// (<c>POST /v1/messages</c>). This is the template the other providers are
/// cloned from. It owns its wire DTOs and never lets provider-specific types
/// escape; callers only ever see <see cref="ChatResult"/>.
/// </summary>
public sealed class AnthropicProvider : IChatProvider
{
    public const string ProviderName = "anthropic";
    public const string HttpClientName = "anthropic";

    private const string MessagesPath = "/v1/messages";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AnthropicProviderOptions _options;
    private readonly ILogger<AnthropicProvider> _logger;

    public AnthropicProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AnthropicProviderOptions> options,
        ILogger<AnthropicProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => ProviderName;

    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        int statusCode;
        string responseBody;
        try
        {
            using var httpRequest = BuildHttpRequest(request);
            HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

            using HttpResponseMessage response = await client.SendAsync(httpRequest, ct);
            statusCode = (int)response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return ChatResult.Fail(NormalizeError(responseBody, statusCode));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cooperative cancellation requested by the caller is not an expected
            // failure — let it propagate so the caller can observe its own cancel.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            // Network error or request timeout (a timeout surfaces here as an
            // OperationCanceledException while our own token is NOT signalled).
            // Rule #1: expected failures return Fail, never throw.
            _logger.LogWarning(ex, "Transport failure calling the Anthropic messages API");
            return ChatResult.Fail(new ChatError(ex.Message, StatusCode: null, ProviderName));
        }

        return BuildResult(responseBody, statusCode);
    }

    private HttpRequestMessage BuildHttpRequest(ChatRequest request)
    {
        string model = string.IsNullOrWhiteSpace(request.Model)
            ? _options.DefaultModel
            : request.Model;

        // Anthropic REQUIRES max_tokens; fall back to the configured default
        // when the caller did not specify one. This fallback lives here, not in
        // the domain ChatRequest.
        int maxTokens = request.MaxTokens ?? _options.DefaultMaxTokens;

        var payload = new AnthropicMessagesRequest
        {
            Model = model,
            MaxTokens = maxTokens,
            Messages = request.Messages
                .Select(message => new AnthropicRequestMessage("user", message.Content))
                .ToList(),
            System = request.System,
            Temperature = request.Temperature
        };

        string json = JsonSerializer.Serialize(payload, SerializerOptions);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri())
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-api-key", _options.ApiKey);
        httpRequest.Headers.Add("anthropic-version", _options.AnthropicVersion);

        return httpRequest;
    }

    private Uri BuildRequestUri()
    {
        string baseUrl = _options.BaseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}{MessagesPath}");
    }

    private ChatResult BuildResult(string responseBody, int statusCode)
    {
        AnthropicMessagesResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AnthropicMessagesResponse>(responseBody, SerializerOptions);
        }
        catch (JsonException ex)
        {
            // 200 OK but the body is not something we can read — no usable answer.
            _logger.LogWarning(ex, "Could not parse a successful Anthropic response body");
            return ChatResult.Fail(new ChatError(
                "Anthropic returned a response that could not be parsed.",
                statusCode,
                ProviderName));
        }

        FinishReason finish = MapFinishReason(parsed?.StopReason);
        string text = ExtractText(parsed);

        // Rule #4: a content-filter / safety stop (or any empty body) is a Fail,
        // not an Ok carrying empty text.
        if (string.IsNullOrWhiteSpace(text))
        {
            string message = finish == FinishReason.ContentFilter
                ? "Anthropic stopped on a content filter / refusal and returned no text."
                : "Anthropic returned no usable text content.";
            return ChatResult.Fail(new ChatError(message, statusCode, ProviderName));
        }

        var usage = parsed?.Usage is { } u
            ? new TokenUsage(u.InputTokens, u.OutputTokens)
            : new TokenUsage(0, 0);

        return ChatResult.Ok(text, usage, finish);
    }

    private ChatError NormalizeError(string responseBody, int statusCode)
    {
        string? message = null;
        try
        {
            AnthropicErrorResponse? error =
                JsonSerializer.Deserialize<AnthropicErrorResponse>(responseBody, SerializerOptions);
            message = error?.Error?.Message;
        }
        catch (JsonException)
        {
            // Non-JSON error body — fall through to the raw-body fallback below.
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = string.IsNullOrWhiteSpace(responseBody)
                ? $"Anthropic request failed with status {statusCode}."
                : responseBody.Trim();
        }

        return new ChatError(message, statusCode, ProviderName);
    }

    private static string ExtractText(AnthropicMessagesResponse? response)
    {
        if (response?.Content is null)
        {
            return string.Empty;
        }

        return string.Concat(response.Content
            .Where(block => block.Type == "text")
            .Select(block => block.Text));
    }

    private static FinishReason MapFinishReason(string? stopReason) => stopReason switch
    {
        "end_turn" => FinishReason.Stop,
        "stop_sequence" => FinishReason.Stop,
        "max_tokens" => FinishReason.MaxTokens,
        "refusal" => FinishReason.ContentFilter,
        _ => FinishReason.Other
    };
}

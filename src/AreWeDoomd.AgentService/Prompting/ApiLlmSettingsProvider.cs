using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Prompting;

public sealed class ApiLlmSettingsProvider : ILlmSettingsProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentServiceOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApiLlmSettingsProvider> _logger;

    private (LlmRuntimeSettings Settings, DateTimeOffset FetchedAt)? _cache;

    public ApiLlmSettingsProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AgentServiceOptions> options,
        TimeProvider timeProvider,
        ILogger<ApiLlmSettingsProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<LlmRuntimeSettings> GetAsync(Guid actingAiUserId, CancellationToken ct)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_cache is { } cached && now - cached.FetchedAt < Ttl)
        {
            return cached.Settings;
        }

        try
        {
            HttpClient client = _httpClientFactory.CreateClient(ContextFetcher.HttpClientName);
            string baseUrl = _options.ApiBaseUrl.TrimEnd('/');

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/agents/llm-settings");
            request.Headers.Add(AgentNotificationHubConstants.SecretHeaderName, _options.SharedSecret);
            request.Headers.Add(AgentImpersonationConstants.UserIdHeaderName, actingAiUserId.ToString());

            using HttpResponseMessage response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(ct);
                var dto = JsonSerializer.Deserialize<LlmSettingsWireDto>(body, SerializerOptions);
                if (dto is not null && !string.IsNullOrWhiteSpace(dto.Model))
                {
                    var settings = new LlmRuntimeSettings(
                        dto.Model, dto.ScoringModel ?? string.Empty, dto.ThinkingEnabled,
                        dto.ScoringTokensPerAccount, dto.CompositionTokensPerPost,
                        dto.ReplyMaxTokens);
                    _cache = (settings, _timeProvider.GetUtcNow());
                    return settings;
                }
            }

            _logger.LogWarning(
                "LLM settings request returned status {StatusCode}", (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch LLM settings");
        }

        return _cache?.Settings ?? Fallback();
    }

    private LlmRuntimeSettings Fallback() => new(
        Model: _options.Model, ScoringModel: string.Empty, ThinkingEnabled: false,
        ScoringTokensPerAccount: 512, CompositionTokensPerPost: 800,
        ReplyMaxTokens: 1024);

    private sealed record LlmSettingsWireDto(
        string? Model, string? ScoringModel, bool ThinkingEnabled,
        int ScoringTokensPerAccount, int CompositionTokensPerPost,
        int ReplyMaxTokens);
}

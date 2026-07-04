using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.AgentService.Context.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Prompting;

public sealed class ApiPersonaProvider : IPersonaProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentServiceOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApiPersonaProvider> _logger;

    private readonly ConcurrentDictionary<Guid, (AgentPersona? Persona, DateTimeOffset FetchedAt)> _cache = new();

    public ApiPersonaProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AgentServiceOptions> options,
        TimeProvider timeProvider,
        ILogger<ApiPersonaProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PersonaResolution> GetAsync(Guid userId, CancellationToken ct)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (_cache.TryGetValue(userId, out var cached) && now - cached.FetchedAt < Ttl)
        {
            return cached.Persona is not null
                ? new PersonaResolution(cached.Persona, PersonaSource.Cache)
                : new PersonaResolution(null, PersonaSource.Default);
        }

        try
        {
            HttpClient client = _httpClientFactory.CreateClient(ContextFetcher.HttpClientName);
            string baseUrl = _options.ApiBaseUrl.TrimEnd('/');
            string url = $"{baseUrl}/api/agents/{userId}/persona";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(AgentNotificationHubConstants.SecretHeaderName, _options.SharedSecret);
            request.Headers.Add(AgentImpersonationConstants.UserIdHeaderName, userId.ToString());

            using HttpResponseMessage response = await client.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _cache[userId] = (null, _timeProvider.GetUtcNow());
                return new PersonaResolution(null, PersonaSource.Default);
            }

            if (response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(ct);
                var dto = JsonSerializer.Deserialize<ApiAgentPersonaResponse>(body, SerializerOptions);

                if (dto is null)
                {
                    _logger.LogWarning("Persona response for user {UserId} could not be deserialized", userId);
                    return GetStaleOrDefault(userId);
                }

                var persona = new AgentPersona(dto.Traits, dto.TypingStyle, dto.Summary, dto.Version);
                _cache[userId] = (persona, _timeProvider.GetUtcNow());
                return new PersonaResolution(persona, PersonaSource.Api);
            }

            _logger.LogWarning(
                "Persona request for user {UserId} returned status {StatusCode}",
                userId,
                (int)response.StatusCode);
            return GetStaleOrDefault(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch persona for user {UserId}", userId);
            return GetStaleOrDefault(userId);
        }
    }

    private PersonaResolution GetStaleOrDefault(Guid userId)
    {
        if (_cache.TryGetValue(userId, out var stale))
        {
            return new PersonaResolution(stale.Persona, PersonaSource.CacheStale);
        }

        return new PersonaResolution(null, PersonaSource.Default);
    }
}

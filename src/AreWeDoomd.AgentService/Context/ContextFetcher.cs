using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Context.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Context;

public sealed class ContextFetcher : IContextFetcher
{
    public const string HttpClientName = "arewedoomd-api";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentServiceOptions _options;
    private readonly ILogger<ContextFetcher> _logger;

    public ContextFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<AgentServiceOptions> options,
        ILogger<ContextFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PostContext?> FetchAsync(Guid postId, string agentUserId, CancellationToken ct)
    {
        HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
        string baseUrl = _options.ApiBaseUrl.TrimEnd('/');

        try
        {
            var post = await GetAsync<ApiPostResponse>(
                client, $"{baseUrl}/api/posts/{postId}", agentUserId, ct);
            if (post is null)
            {
                return null;
            }

            var commentList = await GetAsync<ApiCommentListResponse>(
                client, $"{baseUrl}/api/posts/{postId}/comments", agentUserId, ct);
            var comments = commentList?.Comments ?? [];

            return new PostContext(
                new PostInfo(post.Id, post.Author.Username, post.Author.UserType, post.Content),
                comments
                    .Select(c => new CommentInfo(
                        c.Id, c.Author.Username, c.Author.UserType, c.Content, c.CreatedAt))
                    .ToList());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to fetch context for post {PostId}", postId);
            return null;
        }
    }

    private async Task<T?> GetAsync<T>(HttpClient client, string url, string agentUserId, CancellationToken ct)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(AgentNotificationHubConstants.SecretHeaderName, _options.SharedSecret);
        request.Headers.Add(AgentImpersonationConstants.UserIdHeaderName, agentUserId);

        using HttpResponseMessage response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Context request {Url} returned status {StatusCode}",
                url,
                (int)response.StatusCode);
            return null;
        }

        string body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body, SerializerOptions);
    }
}

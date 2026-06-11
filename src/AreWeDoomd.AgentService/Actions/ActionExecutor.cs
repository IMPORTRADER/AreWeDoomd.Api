using System.Text;
using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Decisions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Actions;

public sealed class ActionExecutor : IActionExecutor
{
    public const string HttpClientName = "arewedoomd-api";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentServiceOptions _options;
    private readonly ILogger<ActionExecutor> _logger;

    public ActionExecutor(
        IHttpClientFactory httpClientFactory,
        IOptions<AgentServiceOptions> options,
        ILogger<ActionExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        AgentDecision decision,
        Guid postId,
        Guid commentId,
        string actingUserId,
        CancellationToken ct)
    {
        if (decision.Action == AgentAction.Ignore)
        {
            _logger.LogInformation(
                "Agent decided to ignore. Reasoning: {Reasoning}",
                decision.Reasoning);
            return;
        }

        string baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        using HttpRequestMessage request = decision.Action switch
        {
            AgentAction.ReplyComment => BuildReplyRequest(baseUrl, postId, decision.Content!),
            AgentAction.LikeComment => new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/api/posts/{postId}/comments/{commentId}/likes"),
            _ => throw new InvalidOperationException($"Unsupported action {decision.Action}.")
        };

        request.Headers.Add(AgentNotificationHubConstants.SecretHeaderName, _options.SharedSecret);
        request.Headers.Add(AgentImpersonationConstants.UserIdHeaderName, actingUserId);

        try
        {
            HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
            using HttpResponseMessage response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Agent action {Action} executed for post {PostId}. Reasoning: {Reasoning}",
                    decision.Action,
                    postId,
                    decision.Reasoning);
            }
            else
            {
                string body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Agent action {Action} failed with status {StatusCode}: {Body}",
                    decision.Action,
                    (int)response.StatusCode,
                    body);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            _logger.LogError(ex, "Agent action {Action} failed on transport", decision.Action);
        }
    }

    private static HttpRequestMessage BuildReplyRequest(string baseUrl, Guid postId, string content)
    {
        string json = JsonSerializer.Serialize(new { content });
        return new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/posts/{postId}/comments")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}

using System.Text;
using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Actions;

public sealed class ScheduleDecisionCallbackClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentServiceOptions> options,
    ILogger<ScheduleDecisionCallbackClient> logger)
{
    public const string HttpClientName = "arewedoomd-api";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<int> RetryableStatusCodes = [429, 502, 503, 504];

    public sealed record CallbackPayload(
        Guid RunItemId, int DesireScore, string? Reasoning, int RequestedPostCount,
        string? ModelUsed, string? ErrorDetail, List<CallbackPost> Posts);

    public sealed record CallbackPost(string Content, DateTimeOffset ScheduledAtUtc);

    public async Task<bool> SubmitAsync(Guid actingAiUserId, CallbackPayload payload, CancellationToken ct)
    {
        string baseUrl = options.Value.ApiBaseUrl.TrimEnd('/');
        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{baseUrl}/api/agent-callbacks/schedule-decision")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
            };
            request.Headers.Add(AgentNotificationHubConstants.SecretHeaderName, options.Value.SharedSecret);
            request.Headers.Add(AgentImpersonationConstants.UserIdHeaderName, actingAiUserId.ToString());

            try
            {
                using var response = await client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                // 409 = karar zaten kayıtlı (sweep yarışı / retry) — başarı sayılır, tekrar denenmez.
                if ((int)response.StatusCode == 409)
                {
                    return true;
                }

                if (!RetryableStatusCodes.Contains((int)response.StatusCode))
                {
                    string body = await response.Content.ReadAsStringAsync(ct);
                    logger.LogError("Schedule decision callback failed with {Status}: {Body}",
                        (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
                    return false;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                logger.LogWarning(ex, "Schedule decision callback transport failure (attempt {Attempt}/3)", attempt);
            }

            await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
        }

        return false;
    }
}

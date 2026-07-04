using System.Text;
using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.AgentService.Ai;

public sealed class AiSessionLogger : IAiSessionLogger
{
    private readonly ILogger<AiSessionLogger> _logger;

    public AiSessionLogger(ILogger<AiSessionLogger> logger)
    {
        _logger = logger;
    }

    public string? Log(string activityId, int attempt, ChatRequest request, ChatResult result)
    {
        string content = BuildSessionContent(activityId, attempt, request, result);

        _logger.LogInformation(
            "AI session {ActivityId} attempt {Attempt}{NewLine}{Content}",
            activityId, attempt, Environment.NewLine, content);

        return WriteSessionFile(activityId, attempt, content);
    }

    private string? WriteSessionFile(string activityId, int attempt, string content)
    {
        try
        {
            string dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string dir = Path.Combine(
                "logs", "ai-sessions",
                dateStr);

            Directory.CreateDirectory(dir);

            string safeId = string.Concat(activityId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string fileName = $"{safeId}_attempt{attempt}_{DateTime.UtcNow:HHmmss}.txt";

            File.WriteAllText(Path.Combine(dir, fileName), content, Encoding.UTF8);

            return $"{dateStr}/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write AI session file for {ActivityId} attempt {Attempt}", activityId, attempt);
            return null;
        }
    }

    private static string BuildSessionContent(string activityId, int attempt, ChatRequest request, ChatResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== AI SESSION ===");
        sb.AppendLine($"Activity : {activityId}");
        sb.AppendLine($"Attempt  : {attempt}");
        sb.AppendLine($"Model    : {request.Model}");
        sb.AppendLine($"Time     : {DateTime.UtcNow:O}");
        sb.AppendLine();

        if (request.System is not null)
        {
            sb.AppendLine("--- SYSTEM ---");
            sb.AppendLine(request.System);
            sb.AppendLine();
        }

        foreach (var msg in request.Messages)
        {
            sb.AppendLine("--- USER ---");
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        sb.AppendLine("--- RESPONSE ---");
        if (result.IsSuccess)
        {
            sb.AppendLine(result.Text);
            sb.AppendLine();
            sb.Append($"Tokens: {result.Usage?.InputTokens} in / {result.Usage?.OutputTokens} out");
            sb.AppendLine($" | Finish: {result.Finish}");
        }
        else
        {
            sb.AppendLine($"ERROR: {result.Error?.Message} (provider={result.Error?.Provider}, status={result.Error?.StatusCode})");
        }

        return sb.ToString();
    }
}

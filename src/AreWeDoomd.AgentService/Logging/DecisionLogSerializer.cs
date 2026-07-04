using System.Text.Json;
using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Logging;

public static class DecisionLogSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public static string Serialize(DecisionLogEntry entry)
    {
        return JsonSerializer.Serialize(entry, Options);
    }
}

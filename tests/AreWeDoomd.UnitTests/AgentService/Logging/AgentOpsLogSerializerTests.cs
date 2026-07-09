using AreWeDoomd.AgentService.Logging;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Logging;

public class AgentOpsLogSerializerTests
{
    [Fact]
    public void Serialize_WhenStatusCodeSet_ShouldIncludeStatusCodeField()
    {
        var entry = new AgentOpsLogEntry(
            DateTimeOffset.Parse("2026-07-09T10:00:00Z"),
            AgentOpsLogLevel.Error, AgentOpsLogSource.LlmProvider,
            "openrouter call failed.", StatusCode: 429);

        var json = AgentOpsLogSerializer.Serialize(entry);

        json.ShouldContain("\"statusCode\":429");
    }

    [Fact]
    public void Serialize_WhenStatusCodeNull_ShouldOmitStatusCodeField()
    {
        var entry = new AgentOpsLogEntry(
            DateTimeOffset.Parse("2026-07-09T10:00:00Z"),
            AgentOpsLogLevel.Info, AgentOpsLogSource.LlmProvider,
            "ok");

        var json = AgentOpsLogSerializer.Serialize(entry);

        json.ShouldNotContain("statusCode");
    }
}

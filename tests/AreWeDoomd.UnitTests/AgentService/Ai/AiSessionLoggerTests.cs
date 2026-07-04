using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Ai;

public sealed class AiSessionLoggerTests
{
    [Fact]
    public void Log_ShouldReturnRelativeSessionRefPointingAtWrittenFile()
    {
        var logger = new AiSessionLogger(NullLogger<AiSessionLogger>.Instance);
        var request = new ChatRequest("model-x", [new ChatMessage("hello")]);
        var result = ChatResult.Ok("hi", new TokenUsage(1, 2), FinishReason.Stop);

        string? sessionRef = logger.Log("act-ref-test", 1, request, result);

        sessionRef.ShouldNotBeNull();
        sessionRef.ShouldStartWith($"{DateTime.UtcNow:yyyy-MM-dd}/");
        string fullPath = Path.Combine("logs", "ai-sessions", sessionRef.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).ShouldBeTrue();
        File.Delete(fullPath);
    }
}

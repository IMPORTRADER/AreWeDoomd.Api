using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Common.Behaviors;

public sealed class LoggingPipelineBehaviorTests
{
    public sealed record TestRequest(string Value) : IRequest<string>;

    [SensitiveProperties]
    public sealed record SensitiveRequest(string Password) : IRequest<string>;

    private static Mock<ILogger<LoggingPipelineBehavior<TRequest, string>>> BuildLogger<TRequest>()
        where TRequest : notnull
        => new();

    [Fact]
    public async Task Handle_ReturnsResultFromNextDelegate()
    {
        var logger = BuildLogger<TestRequest>();
        var behavior = new LoggingPipelineBehavior<TestRequest, string>(logger.Object);
        var request = new TestRequest("hello");

        var result = await behavior.Handle(request, () => Task.FromResult("world"), CancellationToken.None);

        result.ShouldBe("world");
    }

    [Fact]
    public async Task Handle_LogsExecutingAndExecuted_OnSuccess()
    {
        var logger = BuildLogger<TestRequest>();
        var behavior = new LoggingPipelineBehavior<TestRequest, string>(logger.Object);

        await behavior.Handle(new TestRequest("x"), () => Task.FromResult("ok"), CancellationToken.None);

        VerifyLogContains(logger, LogLevel.Information, "Executing", Times.Once());
        VerifyLogContains(logger, LogLevel.Information, "Executed", Times.Once());
    }

    [Fact]
    public async Task Handle_RedactsRequest_WhenSensitivePropertiesAttributePresent()
    {
        var logger = BuildLogger<SensitiveRequest>();
        var behavior = new LoggingPipelineBehavior<SensitiveRequest, string>(logger.Object);

        await behavior.Handle(new SensitiveRequest("secret123"), () => Task.FromResult("ok"), CancellationToken.None);

        VerifyLogContains(logger, LogLevel.Information, "[REDACTED]", Times.Once());
    }

    [Fact]
    public async Task Handle_LogsErrorAndRethrows_WhenNextThrows()
    {
        var logger = BuildLogger<TestRequest>();
        var behavior = new LoggingPipelineBehavior<TestRequest, string>(logger.Object);
        var exception = new InvalidOperationException("boom");

        var act = async () => await behavior.Handle(
            new TestRequest("x"),
            () => throw exception,
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidOperationException>();
        VerifyLogContains(logger, LogLevel.Error, "Failed", Times.Once());
    }

    private static void VerifyLogContains<T>(
        Mock<ILogger<T>> loggerMock,
        LogLevel level,
        string containsText,
        Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(containsText)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}

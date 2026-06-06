using System.Net;
using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.AgentService.Ai.Providers.Anthropic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Ai;

public sealed class AnthropicProviderTests
{
    private static readonly ChatRequest SampleRequest = new(
        Model: "claude-sonnet-4-6",
        Messages: [new ChatMessage("Are we doomed?")],
        System: "You are concise.",
        MaxTokens: 256,
        Temperature: 0.7);

    [Fact]
    public async Task CompleteAsync_WhenProviderReturns429_ShouldReturnFailWithoutThrowing()
    {
        var body = """
        {"type":"error","error":{"type":"rate_limit_error","message":"Number of requests has exceeded your rate limit."}}
        """;
        var response = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent(body)
        };
        var provider = CreateProvider(StubHttpMessageHandler.RespondWith(response));

        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.StatusCode.ShouldBe(429);
        result.Error.Provider.ShouldBe("anthropic");
        result.Error.Message.ShouldContain("rate limit");
        result.Text.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_WhenTransportFails_ShouldReturnFailWithoutThrowing()
    {
        var handler = StubHttpMessageHandler.Throw(
            new HttpRequestException("No such host is known."));
        var provider = CreateProvider(handler);

        // A throw here fails the test — proving CompleteAsync swallows the
        // transport exception and returns a normalized failure instead.
        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Provider.ShouldBe("anthropic");
        result.Error.StatusCode.ShouldBeNull();
        result.Text.ShouldBeNull();
    }

    private static AnthropicProvider CreateProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var options = Options.Create(new AnthropicProviderOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.anthropic.com",
            DefaultModel = "claude-sonnet-4-6",
            DefaultMaxTokens = 1024
        });

        return new AnthropicProvider(
            factory.Object,
            options,
            NullLogger<AnthropicProvider>.Instance);
    }
}

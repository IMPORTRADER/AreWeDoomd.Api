using System.Net;
using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.AgentService.Ai.Providers.Gemini;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Ai;

public sealed class GeminiProviderTests
{
    private static readonly ChatRequest SampleRequest = new(
        Model: "gemini-2.5-flash",
        Messages: [new ChatMessage("Are we doomed?")],
        System: "You are concise.",
        MaxTokens: 256,
        Temperature: 0.7);

    [Fact]
    public async Task CompleteAsync_WhenProviderReturns429_ShouldReturnFailWithoutThrowing()
    {
        var body = """
        {"error":{"code":429,"message":"Resource has been exhausted (e.g. check quota).","status":"RESOURCE_EXHAUSTED"}}
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
        result.Error.Provider.ShouldBe("gemini");
        result.Error.Message.ShouldContain("exhausted");
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
        result.Error!.Provider.ShouldBe("gemini");
        result.Error.StatusCode.ShouldBeNull();
        result.Text.ShouldBeNull();
    }

    private static GeminiProvider CreateProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var options = Options.Create(new GeminiProviderOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://generativelanguage.googleapis.com",
            DefaultModel = "gemini-2.5-flash",
            DefaultMaxTokens = 1024
        });

        return new GeminiProvider(
            factory.Object,
            options,
            NullLogger<GeminiProvider>.Instance);
    }
}

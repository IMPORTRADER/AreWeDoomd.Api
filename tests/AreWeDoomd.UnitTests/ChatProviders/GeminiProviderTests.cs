using System.Net;
using AreWeDoomd.ChatProviders;
using AreWeDoomd.ChatProviders.Providers.Gemini;
using AreWeDoomd.UnitTests.AgentService.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.ChatProviders;

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
        const string body = """
        {"error":{"code":429,"message":"Resource has been exhausted (e.g. check quota).","status":"RESOURCE_EXHAUSTED"}}
        """;
        // MaxRetries=0 → single attempt, no retry delay.
        var provider = CreateProvider(
            StubHttpMessageHandler.AlwaysRespondWith(() => new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent(body)
            }),
            maxRetries: 0);

        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.StatusCode.ShouldBe(429);
        result.Error.Provider.ShouldBe("gemini");
        result.Error.Message.ShouldContain("exhausted");
        result.Text.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_WhenProviderReturns503Consistently_ShouldRetryAndReturnFail()
    {
        const string body = """
        {"error":{"code":503,"message":"This model is currently experiencing high demand.","status":"UNAVAILABLE"}}
        """;
        int callCount = 0;
        var handler = StubHttpMessageHandler.AlwaysRespondWith(() =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(body)
            };
        });
        var provider = CreateProvider(handler, maxRetries: 2, retryBaseDelayMs: 0);

        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Provider.ShouldBe("gemini");
        callCount.ShouldBe(3); // 1 initial + 2 retries
    }

    [Fact]
    public async Task CompleteAsync_WhenProviderReturns503ThenSucceeds_ShouldReturnSuccess()
    {
        const string errorBody = """
        {"error":{"code":503,"message":"This model is currently experiencing high demand.","status":"UNAVAILABLE"}}
        """;
        const string okBody = """
        {"candidates":[{"content":{"parts":[{"text":"We are fine."}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5}}
        """;
        int callCount = 0;
        var handler = StubHttpMessageHandler.AlwaysRespondWith(() =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent(errorBody) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(okBody) };
        });
        var provider = CreateProvider(handler, maxRetries: 2, retryBaseDelayMs: 0);

        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Text.ShouldBe("We are fine.");
        callCount.ShouldBe(2);
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

    [Fact]
    public async Task CompleteAsync_WhenJsonResponseSchemaSet_ShouldSendResponseMimeTypeAndSchema()
    {
        string? requestBody = null;
        var responseJson = """
        {"candidates":[{"content":{"parts":[{"text":"{\"action\":\"ignore\",\"reasoning\":\"ok\"}"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":1}}
        """;
        var handler = new StubHttpMessageHandler(async (req, _) =>
        {
            requestBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };
        });
        var provider = CreateProvider(handler);
        var request = SampleRequest with
        {
            JsonResponseSchema = """{"type":"object","properties":{"action":{"type":"string"}}}"""
        };

        var result = await provider.CompleteAsync(request, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("\"responseMimeType\":\"application/json\"");
        requestBody.ShouldContain("\"responseSchema\"");
    }

    [Fact]
    public async Task CompleteAsync_WhenNoJsonResponseSchema_ShouldNotSendResponseMimeType()
    {
        string? requestBody = null;
        var responseJson = """
        {"candidates":[{"content":{"parts":[{"text":"hello"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":1}}
        """;
        var handler = new StubHttpMessageHandler(async (req, _) =>
        {
            requestBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };
        });
        var provider = CreateProvider(handler);

        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        requestBody.ShouldNotBeNull();
        requestBody.ShouldNotContain("responseMimeType");
    }

    private static GeminiProvider CreateProvider(
        HttpMessageHandler handler,
        int maxRetries = 3,
        int retryBaseDelayMs = 2000)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var options = Options.Create(new GeminiProviderOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://generativelanguage.googleapis.com",
            DefaultModel = "gemini-2.5-flash",
            DefaultMaxTokens = 1024,
            MaxRetries = maxRetries,
            RetryBaseDelayMs = retryBaseDelayMs
        });

        return new GeminiProvider(
            factory.Object,
            options,
            NullLogger<GeminiProvider>.Instance);
    }
}

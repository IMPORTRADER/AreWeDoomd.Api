using System.Net;
using AreWeDoomd.ChatProviders;
using AreWeDoomd.ChatProviders.Providers.OpenRouter;
using AreWeDoomd.UnitTests.AgentService.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.ChatProviders;

public sealed class OpenRouterProviderTests
{
    private static readonly ChatRequest SampleRequest = new(
        Model: "openai/gpt-4o-mini",
        Messages: [new ChatMessage("Are we doomed?")],
        System: "You are concise.",
        MaxTokens: 256,
        Temperature: 0.7);

    [Fact]
    public async Task CompleteAsync_WhenProviderReturns429_ShouldReturnFailWithoutThrowing()
    {
        const string body = """
        {"error":{"message":"Rate limit exceeded, please try again later.","code":429}}
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
        result.Error.Provider.ShouldBe("openrouter");
        result.Error.Message.ShouldContain("Rate limit exceeded");
        result.Text.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_WhenProviderReturns503Consistently_ShouldRetryAndReturnFail()
    {
        const string body = """
        {"error":{"message":"The model is temporarily unavailable due to high demand.","code":503}}
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
        result.Error!.Provider.ShouldBe("openrouter");
        callCount.ShouldBe(3); // 1 initial + 2 retries
    }

    [Fact]
    public async Task CompleteAsync_WhenProviderReturns503ThenSucceeds_ShouldReturnSuccess()
    {
        const string errorBody = """
        {"error":{"message":"The model is temporarily unavailable due to high demand.","code":503}}
        """;
        const string okBody = """
        {"choices":[{"message":{"role":"assistant","content":"We are fine."},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5}}
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
        result.Error!.Provider.ShouldBe("openrouter");
        result.Error.StatusCode.ShouldBeNull();
        result.Text.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_WhenSystemPromptSet_ShouldPrependSystemRoleMessage()
    {
        string? requestBody = null;
        const string okBody = """
        {"choices":[{"message":{"role":"assistant","content":"hello"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1}}
        """;
        var handler = new StubHttpMessageHandler(async (req, _) =>
        {
            requestBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(okBody)
            };
        });
        var provider = CreateProvider(handler);

        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        requestBody.ShouldNotBeNull();
        requestBody.ShouldStartWith("""{"model":"openai/gpt-4o-mini","messages":[{"role":"system","content":"You are concise."},{"role":"user","content":"Are we doomed?"}]""");
    }

    [Fact]
    public async Task CompleteAsync_WhenJsonResponseSchemaSet_ShouldSendResponseFormat()
    {
        string? requestBody = null;
        const string okBody = """
        {"choices":[{"message":{"role":"assistant","content":"{\"action\":\"ignore\"}"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1}}
        """;
        var handler = new StubHttpMessageHandler(async (req, _) =>
        {
            requestBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(okBody)
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
        requestBody.ShouldContain("\"response_format\"");
        requestBody.ShouldContain("\"json_schema\"");
    }

    [Fact]
    public async Task CompleteAsync_WhenNoJsonResponseSchema_ShouldNotSendResponseFormat()
    {
        string? requestBody = null;
        const string okBody = """
        {"choices":[{"message":{"role":"assistant","content":"hello"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1}}
        """;
        var handler = new StubHttpMessageHandler(async (req, _) =>
        {
            requestBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(okBody)
            };
        });
        var provider = CreateProvider(handler);

        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        requestBody.ShouldNotBeNull();
        requestBody.ShouldNotContain("response_format");
    }

    [Fact]
    public async Task CompleteAsync_WhenReasoningDisabled_ShouldSendReasoningEnabledFalse()
    {
        string? capturedBody = null;
        var provider = CreateProvider(
            StubHttpMessageHandler.AlwaysRespondWith(request =>
            {
                capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {"choices":[{"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
                     "usage":{"prompt_tokens":10,"completion_tokens":2}}
                    """)
                };
            }),
            maxRetries: 0);

        await provider.CompleteAsync(SampleRequest with { ReasoningEnabled = false }, CancellationToken.None);

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("\"reasoning\":{\"enabled\":false}");
    }

    [Fact]
    public async Task CompleteAsync_WhenReasoningNull_ShouldNotSendReasoningField()
    {
        string? capturedBody = null;
        var provider = CreateProvider(
            StubHttpMessageHandler.AlwaysRespondWith(request =>
            {
                capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {"choices":[{"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
                     "usage":{"prompt_tokens":10,"completion_tokens":2}}
                    """)
                };
            }),
            maxRetries: 0);

        await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldNotContain("\"reasoning\"");
    }

    [Fact]
    public async Task CompleteAsync_WhenFinishLengthAndNoText_ShouldReportTokenBudgetExhausted()
    {
        const string body = """
        {"choices":[{"message":{"role":"assistant","content":""},"finish_reason":"length"}],
         "usage":{"prompt_tokens":100,"completion_tokens":150}}
        """;
        var provider = CreateProvider(
            StubHttpMessageHandler.AlwaysRespondWith(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            }),
            maxRetries: 0);

        var result = await provider.CompleteAsync(SampleRequest, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldStartWith("Token budget exhausted");
    }

    private static OpenRouterProvider CreateProvider(
        HttpMessageHandler handler,
        int maxRetries = 3,
        int retryBaseDelayMs = 2000)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var options = Options.Create(new OpenRouterProviderOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://openrouter.ai/api/v1",
            DefaultModel = "openai/gpt-4o-mini",
            DefaultMaxTokens = 1024,
            MaxRetries = maxRetries,
            RetryBaseDelayMs = retryBaseDelayMs
        });

        return new OpenRouterProvider(
            factory.Object,
            options,
            NullLogger<OpenRouterProvider>.Instance);
    }
}

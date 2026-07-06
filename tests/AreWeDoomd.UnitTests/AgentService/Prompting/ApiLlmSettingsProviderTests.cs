using System.Net;
using System.Text.Json;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Prompting;
using AreWeDoomd.UnitTests.AgentService.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Prompting;

public sealed class ApiLlmSettingsProviderTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string ApiBaseUrl = "http://localhost:5188";
    private const string SharedSecret = "test-secret";

    private static string SampleSettingsJson => JsonSerializer.Serialize(new
    {
        model = "openai/gpt-oss-120b:free",
        scoringModel = "",
        thinkingEnabled = false,
        scoringTokensPerAccount = 512,
        compositionTokensPerPost = 800,
        personaTokensPerPersona = 700,
        replyMaxTokens = 1024,
        updatedAt = "2026-07-06T12:00:00Z"
    });

    private static ApiLlmSettingsProvider CreateProvider(
        StubHttpMessageHandler handler,
        FakeTimeProvider timeProvider,
        string configModel = "config-model")
    {
        var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var options = Options.Create(new AgentServiceOptions
        {
            ApiBaseUrl = ApiBaseUrl,
            SharedSecret = SharedSecret,
            Model = configModel
        });
        return new ApiLlmSettingsProvider(factory, options, timeProvider, NullLogger<ApiLlmSettingsProvider>.Instance);
    }

    [Fact]
    public async Task GetAsync_ShouldCacheForSixtySeconds()
    {
        int requestCount = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleSettingsJson)
            });
        });
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        var result = await provider.GetAsync(UserId, CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        var result2 = await provider.GetAsync(UserId, CancellationToken.None);

        result.Model.ShouldBe("openai/gpt-oss-120b:free");
        result2.Model.ShouldBe("openai/gpt-oss-120b:free");
        requestCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_WhenApiFailsAndNoCache_ShouldReturnConfigDefaults()
    {
        var handler = StubHttpMessageHandler.RespondWith(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider, configModel: "config-model");

        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Model.ShouldBe("config-model");
        result.ReplyMaxTokens.ShouldBe(1024);
        result.ThinkingEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_WhenApiFailsWithStaleCache_ShouldReturnStaleValue()
    {
        bool returnError = false;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            if (returnError)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleSettingsJson)
            });
        });
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        // Populate the cache with a successful call
        await provider.GetAsync(UserId, CancellationToken.None);

        // Advance past TTL then simulate API failure
        timeProvider.Advance(TimeSpan.FromSeconds(61));
        returnError = true;
        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Model.ShouldBe("openai/gpt-oss-120b:free");
        result.ReplyMaxTokens.ShouldBe(1024);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }
}

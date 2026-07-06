using System.Net;
using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.AgentService.Prompting;
using AreWeDoomd.UnitTests.AgentService.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Prompting;

public sealed class ApiPersonaProviderTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string ApiBaseUrl = "http://localhost:5188";
    private const string SharedSecret = "test-secret";

    private static string PersonaUrl => $"{ApiBaseUrl}/api/agents/{UserId}/persona";

    private static string SamplePersonaJson => JsonSerializer.Serialize(new
    {
        userId = UserId,
        traits = new[] { "curious", "friendly" },
        typingStyle = "casual",
        summary = "A curious and friendly AI",
        version = 1
    });

    private static ApiPersonaProvider CreateProvider(
        StubHttpMessageHandler handler,
        FakeTimeProvider timeProvider)
    {
        var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var options = Options.Create(new AgentServiceOptions
        {
            ApiBaseUrl = ApiBaseUrl,
            SharedSecret = SharedSecret
        });
        return new ApiPersonaProvider(factory, options, timeProvider, NullLogger<ApiPersonaProvider>.Instance);
    }

    [Fact]
    public async Task GetAsync_FirstCall_ShouldFetchFromApiWithAgentHeaders()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler((req, _) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SamplePersonaJson)
            });
        });
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Source.ShouldBe(PersonaSource.Api);
        result.Persona.ShouldNotBeNull();
        result.Persona!.Traits.ShouldBe(["curious", "friendly"]);
        result.Persona.TypingStyle.ShouldBe("casual");
        result.Persona.Summary.ShouldBe("A curious and friendly AI");
        result.Persona.Version.ShouldBe(1);

        capturedRequest.ShouldNotBeNull();
        capturedRequest!.RequestUri!.ToString().ShouldBe(PersonaUrl);
        capturedRequest.Headers.GetValues(AgentNotificationHubConstants.SecretHeaderName).ShouldContain(SharedSecret);
        capturedRequest.Headers.GetValues(AgentImpersonationConstants.UserIdHeaderName).ShouldContain(UserId.ToString());
    }

    [Fact]
    public async Task GetAsync_WithinTtl_ShouldServeFromCacheWithoutSecondRequest()
    {
        int requestCount = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SamplePersonaJson)
            });
        });
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        await provider.GetAsync(UserId, CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Source.ShouldBe(PersonaSource.Cache);
        requestCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_AfterTtlExpiry_ShouldRefetch()
    {
        int requestCount = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SamplePersonaJson)
            });
        });
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        await provider.GetAsync(UserId, CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromSeconds(61));
        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Source.ShouldBe(PersonaSource.Api);
        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_When404_ShouldReturnDefaultAndCacheIt()
    {
        int requestCount = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Source.ShouldBe(PersonaSource.Default);
        result.Persona.ShouldBeNull();

        var result2 = await provider.GetAsync(UserId, CancellationToken.None);
        result2.Source.ShouldBe(PersonaSource.Default);
        result2.Persona.ShouldBeNull();
        requestCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_WhenServerErrorAfterSuccessfulFetch_ShouldServeStale()
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
                Content = new StringContent(SamplePersonaJson)
            });
        });
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        await provider.GetAsync(UserId, CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromSeconds(61));
        returnError = true;
        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Source.ShouldBe(PersonaSource.CacheStale);
        result.Persona.ShouldNotBeNull();
        result.Persona!.Traits.ShouldBe(["curious", "friendly"]);
    }

    [Fact]
    public async Task GetAsync_WhenServerErrorWithNoCache_ShouldReturnDefault()
    {
        var handler = StubHttpMessageHandler.RespondWith(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Source.ShouldBe(PersonaSource.Default);
        result.Persona.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_WhenResponseMissingTraits_ShouldReturnDefaultNotThrow()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var payload = new { userId = UserId, typingStyle = "x", summary = "y", version = 1 };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            });
        });
        var timeProvider = new FakeTimeProvider();
        var provider = CreateProvider(handler, timeProvider);

        var result = await provider.GetAsync(UserId, CancellationToken.None);

        result.Source.ShouldBe(PersonaSource.Default);
        result.Persona.ShouldBeNull();
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

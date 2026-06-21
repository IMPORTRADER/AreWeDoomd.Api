using System.Net;
using System.Net.Http;
using AreWeDoomd.IntegrationTests.Realtime;
using Shouldly;
using Xunit;

namespace AreWeDoomd.IntegrationTests.Profile;

public sealed class FollowEndpointsAuthTests : IClassFixture<AgentHubTestFactory>
{
    private readonly HttpClient _client;
    public FollowEndpointsAuthTests(AgentHubTestFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Follow_WhenNoToken_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsync("/api/users/driftwood/follow", content: null);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Followers_WhenNoToken_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/users/driftwood/followers");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProfileByUsername_WhenNoToken_ShouldNotReturnUnauthorized()
    {
        // public endpoint: anonymous must not be 401 (404/200/503 acceptable depending on DB)
        var response = await _client.GetAsync("/api/users/driftwood");
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }
}

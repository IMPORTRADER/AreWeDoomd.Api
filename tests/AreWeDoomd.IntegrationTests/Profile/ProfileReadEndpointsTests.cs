using System.Net;
using AreWeDoomd.IntegrationTests.Realtime;
using Shouldly;
using Xunit;

namespace AreWeDoomd.IntegrationTests.Profile;

public sealed class ProfileReadEndpointsTests : IClassFixture<AgentHubTestFactory>
{
    private readonly HttpClient _client;

    public ProfileReadEndpointsTests(AgentHubTestFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetMe_WhenNoToken_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/users/me");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetByUsername_WhenUserMissing_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"/api/users/nope_{Guid.NewGuid():N}".Substring(0, 24));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

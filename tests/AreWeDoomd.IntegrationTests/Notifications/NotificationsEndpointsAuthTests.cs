using System.Net;
using AreWeDoomd.IntegrationTests.Realtime;
using Shouldly;
using Xunit;

namespace AreWeDoomd.IntegrationTests.Notifications;

public sealed class NotificationsEndpointsAuthTests : IClassFixture<AgentHubTestFactory>
{
    private readonly HttpClient _client;

    public NotificationsEndpointsAuthTests(AgentHubTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetNotifications_WhenNoToken_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/notifications");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUnreadCount_WhenNoToken_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/notifications/unread-count");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAllRead_WhenNoToken_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsync("/api/notifications/mark-all-read", content: null);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

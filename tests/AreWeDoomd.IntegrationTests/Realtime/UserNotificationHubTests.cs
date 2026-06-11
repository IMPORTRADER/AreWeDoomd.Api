using AreWeDoomd.Api.Realtime;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Dispatching;
using AreWeDoomd.Domain.Users;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AreWeDoomd.IntegrationTests.Realtime;

public sealed class UserNotificationHubTests : IClassFixture<AgentHubTestFactory>
{
    private readonly AgentHubTestFactory _factory;

    public UserNotificationHubTests(AgentHubTestFactory factory)
    {
        _factory = factory;
    }

    private (HubConnection connection, Guid userId) BuildConnection()
    {
        var user = User.Create(
            username: $"u{Guid.NewGuid():N}".Substring(0, 12),
            email: $"{Guid.NewGuid():N}@example.com",
            passwordHash: "integration-test-password-hash-placeholder",
            userType: UserType.Human,
            now: DateTimeOffset.UtcNow);

        using var scope = _factory.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var token = tokenGenerator.Generate(user);

        var server = _factory.Server;
        var hubUri = new Uri(server.BaseAddress, UserNotificationHubConstants.HubPath.TrimStart('/'));

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        return (connection, user.Id);
    }

    [Fact]
    public async Task Connect_WhenTokenValid_ShouldReceiveNotificationTargetedAtUser()
    {
        var (connection, userId) = BuildConnection();
        await using var _ = connection;

        var tcs = new TaskCompletionSource<UserNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<UserNotificationDto>(
            UserNotificationHubConstants.ReceiveNotificationMethod,
            dto => tcs.TrySetResult(dto));

        await connection.StartAsync();

        var sender = _factory.Services.GetRequiredService<IUserHubSender>();
        var sent = new UserNotificationDto(
            Id: Guid.NewGuid(),
            Template: "post.comment.created",
            Params: new Dictionary<string, string> { ["actor_name"] = "MiraStone" },
            ActorName: "MiraStone",
            ActorType: "Human",
            CreatedAt: DateTimeOffset.UtcNow,
            IsRead: false);

        await sender.SendAsync(userId, sent);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(tcs.Task, "notification was not received within timeout");

        var received = await tcs.Task;
        received.Template.ShouldBe(sent.Template);
        received.ActorName.ShouldBe(sent.ActorName);
        received.Params["actor_name"].ShouldBe("MiraStone");
    }

    [Fact]
    public async Task Connect_WhenNoToken_ShouldBeRejected()
    {
        var server = _factory.Server;
        var hubUri = new Uri(server.BaseAddress, UserNotificationHubConstants.HubPath.TrimStart('/'));

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        await Should.ThrowAsync<Exception>(async () => await connection.StartAsync());
    }

    [Fact]
    public async Task Send_WhenTargetingDifferentUser_ShouldNotDeliverToOtherConnection()
    {
        var (connection, userId) = BuildConnection();
        await using var _ = connection;

        var received = false;
        connection.On<UserNotificationDto>(
            UserNotificationHubConstants.ReceiveNotificationMethod,
            _ => received = true);

        await connection.StartAsync();

        var sender = _factory.Services.GetRequiredService<IUserHubSender>();
        var otherUserId = Guid.NewGuid();
        var dto = new UserNotificationDto(
            Guid.NewGuid(), "post.comment.created", new Dictionary<string, string>(),
            "Someone", "Human", DateTimeOffset.UtcNow, false);

        await sender.SendAsync(otherUserId, dto);

        await Task.Delay(TimeSpan.FromSeconds(1));
        received.ShouldBeFalse();
    }
}

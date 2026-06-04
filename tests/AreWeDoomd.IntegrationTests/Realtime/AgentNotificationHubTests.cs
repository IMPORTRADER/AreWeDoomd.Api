using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.ActivityNotifications.Contracts;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AreWeDoomd.IntegrationTests.Realtime;

public sealed class AgentNotificationHubTests : IClassFixture<AgentHubTestFactory>
{
    private const string Secret = AgentHubTestFactory.SharedSecret;
    private readonly AgentHubTestFactory _factory;

    public AgentNotificationHubTests(AgentHubTestFactory factory)
    {
        _factory = factory;
    }

    private HubConnection BuildConnection(string secret)
    {
        var server = _factory.Server;
        var hubUri = new Uri(server.BaseAddress, AgentNotificationHubConstants.HubPath.TrimStart('/'));

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Headers.Add(AgentNotificationHubConstants.SecretHeaderName, secret);
            })
            .AddMessagePackProtocol(opts =>
            {
                opts.SerializerOptions = MessagePackSerializerOptions.Standard
                    .WithResolver(ContractlessStandardResolver.Instance);
            })
            .Build();
    }

    [Fact]
    public async Task ReceivesNotification_WhenSecretValid()
    {
        await using var connection = BuildConnection(Secret);

        var tcs = new TaskCompletionSource<ActivityNotification>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<ActivityNotification>(
            AgentNotificationHubConstants.ReceiveEventMethod,
            notification => tcs.TrySetResult(notification));

        await connection.StartAsync();

        var notifier = _factory.Services.GetRequiredService<IAgentNotifier>();
        var recipientId = Guid.NewGuid().ToString();
        var sent = new ActivityNotification(
            ActivityId: "test_act_1",
            ActivityType: ActivityType.CommentCreated,
            OccurredAt: DateTimeOffset.UtcNow,
            Actor: new ActivityActor(Guid.NewGuid().ToString(), ActorType.Human, "TestUser"),
            Object: new ActivityObject(Guid.NewGuid().ToString(), ActivityObjectType.Comment, "test comment"),
            Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post, recipientId),
            Recipients: [
                new NotificationRecipient(
                    UserId: recipientId,
                    Reason: NotificationReason.PostOwner,
                    Template: "post.comment.created",
                    Params: new Dictionary<string, string> { ["actor_name"] = "TestUser" },
                    DedupeKey: "test:dedupe:1",
                    Priority: NotificationPriority.Normal)
            ]);

        await notifier.NotifyAsync(sent);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(tcs.Task, "notification was not received within timeout");

        var received = await tcs.Task;
        received.ActivityId.ShouldBe(sent.ActivityId);
        received.ActivityType.ShouldBe(sent.ActivityType);
        received.Actor.DisplayName.ShouldBe(sent.Actor.DisplayName);
        received.Target.OwnerId.ShouldBe(sent.Target.OwnerId);
        received.Recipients.Count.ShouldBe(1);
        received.Recipients[0].Priority.ShouldBe(NotificationPriority.Normal);
        received.Recipients[0].Params["actor_name"].ShouldBe("TestUser");
    }

    [Fact]
    public async Task ConnectionRejected_WhenSecretInvalid()
    {
        await using var connection = BuildConnection("wrong-secret");

        await Should.ThrowAsync<Exception>(async () => await connection.StartAsync());
    }
}

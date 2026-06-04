using MessagePack;
using AreWeDoomd.EventNotifications.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService;

public sealed class AgentNotificationListener : BackgroundService
{
    private readonly AgentServiceOptions _options;
    private readonly ILogger<AgentNotificationListener> _logger;
    private HubConnection? _connection;

    public AgentNotificationListener(
        IOptions<AgentServiceOptions> options,
        ILogger<AgentNotificationListener> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl, options =>
            {
                options.Headers.Add(
                    AgentNotificationHubConstants.SecretHeaderName,
                    _options.SharedSecret);
            })
            .AddMessagePackProtocol(opts =>
            {
                opts.SerializerOptions = MessagePackSerializerOptions.Standard
                    .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<EventNotification>(
            AgentNotificationHubConstants.ReceiveEventMethod,
            notification =>
            {
                _logger.LogInformation(
                    "Received event {ActivityType} from actor {ActorId} with {RecipientCount} recipient(s)",
                    notification.ActivityType,
                    notification.Actor.Id,
                    notification.Recipients.Count);
            });

        await ConnectWithRetryAsync(stoppingToken);
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connection!.StartAsync(stoppingToken);
                _logger.LogInformation(
                    "Connected to agent notification hub at {HubUrl}",
                    _options.HubUrl);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to connect to agent notification hub, retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}

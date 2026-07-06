using MessagePack;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Logging;
using AreWeDoomd.AgentService.Processing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService;

public sealed class AgentNotificationListener : BackgroundService
{
    private readonly AgentServiceOptions _options;
    private readonly AgentEventQueue _queue;
    private readonly ScheduleRunQueue _scheduleQueue;
    private readonly IDecisionLogWriter _decisionLog;
    private readonly IAgentOpsLogWriter _opsLog;
    private readonly ILogger<AgentNotificationListener> _logger;
    private HubConnection? _connection;

    public AgentNotificationListener(
        IOptions<AgentServiceOptions> options,
        AgentEventQueue queue,
        ScheduleRunQueue scheduleQueue,
        IDecisionLogWriter decisionLog,
        IAgentOpsLogWriter opsLog,
        ILogger<AgentNotificationListener> logger)
    {
        _options = options.Value;
        _queue = queue;
        _scheduleQueue = scheduleQueue;
        _decisionLog = decisionLog;
        _opsLog = opsLog;
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

        _connection.On<ActivityNotification>(
            AgentNotificationHubConstants.ReceiveEventMethod,
            HandleNotification);

        _connection.On<ScheduleRunRequest>(
            AgentNotificationHubConstants.ReceiveScheduleRunMethod,
            request =>
            {
                if (!_scheduleQueue.TryEnqueue(request))
                {
                    _logger.LogWarning(
                        "Schedule run queue rejected run {RunId}; it was dropped (API sweep will re-push).",
                        request.RunId);
                }
            });

        _connection.Reconnecting += ex =>
        {
            _logger.LogWarning("Connection lost, reconnecting to agent notification hub...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Reconnected to agent notification hub.");
            return Task.CompletedTask;
        };

        await ConnectWithRetryAsync(stoppingToken);

        if (!stoppingToken.IsCancellationRequested)
            _logger.LogInformation("Listening for activity notifications.");
    }

    public void HandleNotification(ActivityNotification notification)
    {
        var agentEvent = AgentEvent.From(notification);
        if (_queue.TryEnqueue(agentEvent))
        {
            _logger.LogInformation(
                "AgentEvent enqueued: {ActivityId} | {ActivityType} | Actor={ActorName}",
                agentEvent.ActivityId,
                agentEvent.ActivityType,
                agentEvent.Actor.DisplayName);
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Info, AgentOpsLogSource.Pipeline,
                $"Event received: {agentEvent.ActivityType} from {agentEvent.Actor.DisplayName}; queued for processing.",
                ActivityId: agentEvent.ActivityId));
        }
        else
        {
            _logger.LogWarning(
                "Agent event queue rejected event {ActivityId}; it was dropped.",
                agentEvent.ActivityId);
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Warning, AgentOpsLogSource.Pipeline,
                $"Event queue full; event {notification.ActivityId} dropped.",
                ActivityId: notification.ActivityId));

            var aiRecipient = notification.Recipients
                .FirstOrDefault(r => r.RecipientType == NotificationRecipientType.Ai);

            _decisionLog.TryLog(new DecisionLogEntry(
                Ts: DateTimeOffset.UtcNow,
                AiUserId: aiRecipient?.UserId ?? string.Empty,
                ActivityId: notification.ActivityId,
                ActivityType: notification.ActivityType.ToString(),
                Outcome: DecisionOutcome.Dropped,
                Priority: aiRecipient?.Priority.ToString()));
        }
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
                    "Failed to connect to agent notification hub ({Message}), retrying in 5 seconds",
                    ex.Message);
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

using AreWeDoomd.Application.Notifications.Dispatching;
using AreWeDoomd.Application.Notifications.Engine;

namespace AreWeDoomd.Api.Notifications;

public sealed class ActivityNotificationPublisherService(
    IActivityNotificationQueue queue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ActivityNotificationPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ActivityContext context;

            try
            {
                context = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await PublishAsync(context, stoppingToken);
        }
    }

    private async Task PublishAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var notificationEngine = scope.ServiceProvider.GetRequiredService<INotificationEngine>();
            var notificationDispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

            var notification = await notificationEngine.ComputeAsync(context, cancellationToken);
            await notificationDispatcher.DispatchAsync(notification, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "ActivityNotificationPublisherService: failed to dispatch activity {ActivityType}.",
                context.ActivityType);
        }
    }
}

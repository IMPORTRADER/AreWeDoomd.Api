using AreWeDoomd.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.Api.Jobs;

public sealed class BulkCreateJobRunner(
    IBulkCreateJobQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BulkCreateJobRunner> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await queue.DequeueAsync(stoppingToken);

                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<BulkCreateJobProcessor>();
                await processor.ProcessAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in BulkCreateJobRunner; continuing.");
            }
        }
    }
}

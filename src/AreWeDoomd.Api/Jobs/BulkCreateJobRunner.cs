using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.Api.Jobs;

public sealed class BulkCreateJobRunner(
    IBulkCreateJobQueue queue,
    IServiceScopeFactory scopeFactory,
    IAgentOpsLogger opsLog,
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
                try
                {
                    await processor.ProcessAsync(jobId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    opsLog.TryLog(new AgentOpsLogRecord(
                        DateTimeOffset.UtcNow, AgentOpsLogLevels.Error, AgentOpsLogSources.Admin,
                        $"Bulk create job {jobId} crashed before completing.",
                        Detail: ex.Message));
                    throw;
                }
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

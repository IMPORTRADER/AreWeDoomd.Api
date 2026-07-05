using AreWeDoomd.Application.Features.PostScheduling.Commands.ProcessDueScheduledPosts;
using AreWeDoomd.Application.Features.PostScheduling.Commands.SweepStaleScheduleRuns;
using MediatR;

namespace AreWeDoomd.Api.Jobs;

/// <summary>
/// Adaptif zamanlayıcı: sabit kısa aralıklı poll YOK — Azure SQL serverless auto-pause'u
/// boş günlerde çalışmaya devam etsin diye bir sonraki due zamanına kadar uyur
/// (üst sınır MaxIdle güvenlik poll'u; yeni kayıt geldiğinde Waker erken uyandırır).
/// </summary>
public sealed class ScheduledPostPublisher(
    IServiceScopeFactory scopeFactory,
    SchedulePublisherWaker waker,
    ILogger<ScheduledPostPublisher> logger)
    : BackgroundService
{
    private static readonly TimeSpan MaxIdle = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan MinSleep = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTimeOffset? nextDue = null;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await mediator.Send(new SweepStaleScheduleRunsCommand(), stoppingToken);
                var result = await mediator.Send(new ProcessDueScheduledPostsCommand(), stoppingToken);
                if (result.IsSuccess)
                {
                    nextDue = result.Value;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Azure SQL "Database not currently available" dahil — döngü asla ölmez.
                logger.LogError(ex, "ScheduledPostPublisher tick failed; continuing.");
            }

            var sleep = MaxIdle;
            if (nextDue is not null)
            {
                var untilDue = nextDue.Value - DateTimeOffset.UtcNow;
                sleep = untilDue < MinSleep ? MinSleep : (untilDue > MaxIdle ? MaxIdle : untilDue);
            }

            try
            {
                await waker.WaitAsync(sleep, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

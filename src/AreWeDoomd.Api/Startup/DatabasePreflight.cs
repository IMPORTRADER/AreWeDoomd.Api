using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Api.Startup;

/// <summary>
/// Blocks startup while probing the database on a configured retry schedule, so an
/// Azure SQL serverless cold start (~1.5 min resume) does not take the API down.
/// Each probe also nudges the paused database to resume. If every attempt fails the
/// API still starts; /health keeps reporting the live database state.
/// </summary>
public static class DatabasePreflight
{
    public static async Task<bool> RunAsync(WebApplication app)
    {
        var options = app.Configuration
            .GetSection(DatabasePreflightOptions.SectionName)
            .Get<DatabasePreflightOptions>() ?? new DatabasePreflightOptions();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabasePreflight");

        if (!options.Enabled)
        {
            logger.LogInformation("Database preflight is disabled by configuration.");
            return true;
        }

        return await ExecuteAsync(
            async cancellationToken =>
            {
                await using var scope = app.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AreWeDoomdDbContext>();
                return await dbContext.Database.CanConnectAsync(cancellationToken);
            },
            options.RetryDelays,
            Task.Delay,
            logger,
            app.Lifetime.ApplicationStopping);
    }

    public static async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task<bool>> probeAsync,
        IReadOnlyList<TimeSpan> retryDelays,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var totalAttempts = retryDelays.Count + 1;

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            var succeeded = false;
            string failureReason = "connection probe returned false";

            try
            {
                succeeded = await probeAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
            }

            if (succeeded)
            {
                logger.LogInformation(
                    "Database preflight succeeded on attempt {Attempt}/{TotalAttempts}.",
                    attempt, totalAttempts);
                return true;
            }

            if (attempt < totalAttempts)
            {
                var delay = retryDelays[attempt - 1];
                logger.LogWarning(
                    "Database preflight attempt {Attempt}/{TotalAttempts} failed ({Reason}); retrying in {Delay}.",
                    attempt, totalAttempts, failureReason, delay);
                await delayAsync(delay, cancellationToken);
            }
        }

        logger.LogError(
            "Database preflight failed after {TotalAttempts} attempts; giving up. " +
            "The API will start, but database-dependent requests will fail until the database is reachable.",
            totalAttempts);
        return false;
    }
}

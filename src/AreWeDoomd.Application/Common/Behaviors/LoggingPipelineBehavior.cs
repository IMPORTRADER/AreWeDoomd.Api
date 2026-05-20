using System.Diagnostics;
using System.Reflection;
using AreWeDoomd.Application.Common.Attributes;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.Application.Common.Behaviors;

public sealed class LoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var isSensitive = typeof(TRequest).GetCustomAttribute<SensitivePropertiesAttribute>() is not null;

        if (isSensitive)
        {
            logger.LogInformation("Executing {RequestName} [REDACTED]", requestName);
        }
        else
        {
            logger.LogInformation("Executing {RequestName} {@Request}", requestName, request);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await next();
            stopwatch.Stop();
            // Do not log the result — auth responses contain access tokens that must not be persisted.
            logger.LogInformation("Executed {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed {RequestName} after {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

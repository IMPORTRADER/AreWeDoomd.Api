using Microsoft.Extensions.Configuration;
using Serilog;

namespace AreWeDoomd.Infrastructure.Common.Logging;

public static class SerilogConfiguration
{
    private const string OutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    public static LoggerConfiguration AddInfrastructureSinks(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        return loggerConfiguration
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.File(
                path: "logs/log-.txt",
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30);
    }
}

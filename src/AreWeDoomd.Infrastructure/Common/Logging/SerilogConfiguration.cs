using System.Collections.ObjectModel;
using System.Data;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Sinks.MSSqlServer;

namespace AreWeDoomd.Infrastructure.Common.Logging;

public static class SerilogConfiguration
{
    public static LoggerConfiguration AddInfrastructureSinks(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AreWeDoomdSql");

        var columnOptions = new ColumnOptions();
        columnOptions.Store.Remove(StandardColumn.Properties);
        columnOptions.Store.Add(StandardColumn.LogEvent);
        columnOptions.AdditionalColumns = new Collection<SqlColumn>
        {
            new() { ColumnName = "SourceContext", DataType = SqlDbType.NVarChar, DataLength = 255, AllowNull = true },
            new() { ColumnName = "RequestPath", DataType = SqlDbType.NVarChar, DataLength = 2048, AllowNull = true },
            new() { ColumnName = "UserId", DataType = SqlDbType.NVarChar, DataLength = 50, AllowNull = true },
            new() { ColumnName = "ElapsedMs", DataType = SqlDbType.BigInt, AllowNull = true },
        };

        return loggerConfiguration
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .WriteTo.MSSqlServer(
                connectionString: connectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "Logs",
                    AutoCreateSqlTable = true,
                },
                columnOptions: columnOptions);
    }
}

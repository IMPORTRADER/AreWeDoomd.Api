using AreWeDoomd.AgentService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.Configure<AgentServiceOptions>(
    builder.Configuration.GetSection(AgentServiceOptions.SectionName));
builder.Services.AddHostedService<AgentNotificationListener>();

var host = builder.Build();
host.Run();

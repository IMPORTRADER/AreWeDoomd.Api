using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Map the conventional GEMINI_API_KEY environment variable onto the provider's
// config key. Added last so it takes precedence over appsettings.json (and the
// dev user-secret above). Only applied when the variable is actually set.
string? geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
if (!string.IsNullOrWhiteSpace(geminiApiKey))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ChatProviders:Gemini:ApiKey"] = geminiApiKey
    });
}

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
builder.Services.AddChatProviders(builder.Configuration);
builder.Services.AddHostedService<AgentNotificationListener>();

var host = builder.Build();
host.Run();

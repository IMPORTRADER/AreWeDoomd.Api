using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.AgentService.Processing;
using AreWeDoomd.AgentService.Prompting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
builder.Services.AddSingleton<IAiSessionLogger, AiSessionLogger>();

builder.Services.AddHttpClient(ContextFetcher.HttpClientName);
builder.Services.AddSingleton<AgentEventQueue>();
builder.Services.AddSingleton<PromptFileSet>();
builder.Services.AddSingleton<AgentProfileStore>();
builder.Services.AddSingleton<IPromptComposer, PromptComposer>();
builder.Services.AddSingleton<DecisionParser>();
builder.Services.AddSingleton<PriorityDecayPolicy>();
builder.Services.AddSingleton<IContextFetcher, ContextFetcher>();
builder.Services.AddSingleton<IActionExecutor, ActionExecutor>();

builder.Services.AddHostedService<AgentNotificationListener>();
builder.Services.AddHostedService(serviceProvider =>
{
    var agentOptions = serviceProvider.GetRequiredService<IOptions<AgentServiceOptions>>();
    return new AgentEventProcessor(
        serviceProvider.GetRequiredService<AgentEventQueue>(),
        serviceProvider.GetRequiredService<IContextFetcher>(),
        serviceProvider.GetRequiredService<PriorityDecayPolicy>(),
        serviceProvider.GetRequiredService<IPromptComposer>(),
        serviceProvider.GetRequiredKeyedService<IChatProvider>(agentOptions.Value.ChatProvider),
        serviceProvider.GetRequiredService<DecisionParser>(),
        serviceProvider.GetRequiredService<IActionExecutor>(),
        serviceProvider.GetRequiredService<IAiSessionLogger>(),
        agentOptions,
        serviceProvider.GetRequiredService<ILogger<AgentEventProcessor>>());
});

var host = builder.Build();
host.Run();

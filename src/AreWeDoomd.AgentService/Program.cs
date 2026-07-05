using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.AgentService.Logging;
using AreWeDoomd.AgentService.Processing;
using AreWeDoomd.AgentService.Prompting;
using AreWeDoomd.ChatProviders;
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

// Map the conventional OPENROUTER_API_KEY environment variable onto the
// provider's config key, same pattern as GEMINI_API_KEY above.
string? openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
if (!string.IsNullOrWhiteSpace(openRouterApiKey))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ChatProviders:OpenRouter:ApiKey"] = openRouterApiKey
    });
}

// Map the conventional DECISION_LOG_ROOT environment variable onto the
// decision log's config key, same pattern as the API-key mappings above.
string? decisionLogRoot = Environment.GetEnvironmentVariable("DECISION_LOG_ROOT");
if (!string.IsNullOrWhiteSpace(decisionLogRoot))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DecisionLog:RootPath"] = decisionLogRoot
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
builder.Services.Configure<DecisionLogOptions>(builder.Configuration.GetSection(DecisionLogOptions.SectionName));
builder.Services.AddChatProviders(builder.Configuration);
builder.Services.AddSingleton<IAiSessionLogger, AiSessionLogger>();

builder.Services.AddHttpClient(ContextFetcher.HttpClientName);
builder.Services.AddSingleton<AgentEventQueue>();
builder.Services.AddSingleton<ScheduleRunQueue>();
builder.Services.AddSingleton<DailyPostPlanParser>();
builder.Services.AddSingleton<ScheduleDecisionCallbackClient>();
builder.Services.AddSingleton<IScheduleDecisionCallbackClient>(
    sp => sp.GetRequiredService<ScheduleDecisionCallbackClient>());
builder.Services.AddSingleton<PromptFileSet>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPersonaProvider, ApiPersonaProvider>();
builder.Services.AddSingleton<ILlmSettingsProvider, ApiLlmSettingsProvider>();
builder.Services.AddSingleton<IPromptComposer, PromptComposer>();
builder.Services.AddSingleton<DecisionParser>();
builder.Services.AddSingleton<PriorityDecayPolicy>();
builder.Services.AddSingleton<IContextFetcher, ContextFetcher>();
builder.Services.AddSingleton<IActionExecutor, ActionExecutor>();

builder.Services.AddSingleton<DecisionLogWriter>();
builder.Services.AddSingleton<IDecisionLogWriter>(sp => sp.GetRequiredService<DecisionLogWriter>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DecisionLogWriter>());

builder.Services.AddHostedService<AgentNotificationListener>();
builder.Services.AddHostedService<DailySchedulePlanner>();
builder.Services.AddHostedService(serviceProvider =>
{
    var agentOptions = serviceProvider.GetRequiredService<IOptions<AgentServiceOptions>>();
    return new AgentEventProcessor(
        serviceProvider.GetRequiredService<AgentEventQueue>(),
        serviceProvider.GetRequiredService<IContextFetcher>(),
        serviceProvider.GetRequiredService<PriorityDecayPolicy>(),
        serviceProvider.GetRequiredService<IPersonaProvider>(),
        serviceProvider.GetRequiredService<IPromptComposer>(),
        serviceProvider.GetRequiredKeyedService<IChatProvider>(agentOptions.Value.ChatProvider),
        serviceProvider.GetRequiredService<DecisionParser>(),
        serviceProvider.GetRequiredService<IActionExecutor>(),
        serviceProvider.GetRequiredService<IAiSessionLogger>(),
        serviceProvider.GetRequiredService<IDecisionLogWriter>(),
        serviceProvider.GetRequiredService<ILlmSettingsProvider>(),
        agentOptions,
        serviceProvider.GetRequiredService<ILogger<AgentEventProcessor>>());
});

var host = builder.Build();
host.Run();

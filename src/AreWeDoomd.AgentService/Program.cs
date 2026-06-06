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

// One-off smoke driver: `dotnet run -- chat-smoke [providerKey]`. Resolves the
// keyed IChatProvider from the real DI container and sends a single prompt, then
// exits — it never starts the SignalR listener. Handy for manually confirming a
// provider talks to its live API.
if (args.Contains("chat-smoke"))
{
    using var smokeHost = builder.Build();

    string providerKey = args.Length > 1 ? args[1] : "gemini";
    var provider = smokeHost.Services.GetRequiredKeyedService<IChatProvider>(providerKey);

    var request = new ChatRequest(
        Model: string.Empty, // empty => provider falls back to its configured DefaultModel
        Messages: [new ChatMessage("Hello Gemini")]);

    Console.WriteLine($"[chat-smoke] Sending \"Hello Gemini\" to provider '{provider.Name}'...");
    ChatResult result = await provider.CompleteAsync(request, CancellationToken.None);

    if (result.IsSuccess)
    {
        Console.WriteLine("[chat-smoke] RESULT: SUCCESS");
        Console.WriteLine($"[chat-smoke]   finish : {result.Finish}");
        Console.WriteLine($"[chat-smoke]   usage  : in={result.Usage?.InputTokens} out={result.Usage?.OutputTokens}");
        Console.WriteLine($"[chat-smoke]   text   : {result.Text}");
    }
    else
    {
        Console.WriteLine("[chat-smoke] RESULT: FAIL");
        Console.WriteLine($"[chat-smoke]   status : {result.Error?.StatusCode?.ToString() ?? "(none)"}");
        Console.WriteLine($"[chat-smoke]   from   : {result.Error?.Provider}");
        Console.WriteLine($"[chat-smoke]   message: {result.Error?.Message}");
    }

    return;
}

var host = builder.Build();
host.Run();

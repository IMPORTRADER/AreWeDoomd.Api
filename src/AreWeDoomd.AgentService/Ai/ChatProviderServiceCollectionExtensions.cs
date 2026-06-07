using AreWeDoomd.AgentService.Ai.Providers.Gemini;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AreWeDoomd.AgentService.Ai;

public static class ChatProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers every chat provider adapter and its supporting HttpClient.
    /// Each provider is keyed by its <see cref="IChatProvider.Name"/> so callers
    /// resolve one with <c>GetRequiredKeyedService&lt;IChatProvider&gt;("anthropic")</c>.
    /// </summary>
    public static IServiceCollection AddChatProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ===== PROVIDER PLUG-IN POINT ===================================
        // Adding a provider later = one new adapter file (clone
        // AnthropicProvider) + one registration line below. Nothing else in
        // this method or the consuming code needs to change.
        AddGeminiProvider(services, configuration);
        // ================================================================

        return services;
    }

    private static void AddGeminiProvider(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<GeminiProviderOptions>()
            .Bind(configuration.GetSection(GeminiProviderOptions.SectionName));

        // Named HttpClient via IHttpClientFactory so a resilience handler can be
        // wrapped around it later (.AddStandardResilienceHandler / Polly) without
        // touching the adapter.
        services.AddHttpClient(GeminiProvider.HttpClientName);

        services.AddKeyedSingleton<IChatProvider, GeminiProvider>(GeminiProvider.ProviderName);
    }
}

using AreWeDoomd.ChatProviders.Providers.Gemini;
using AreWeDoomd.ChatProviders.Providers.OpenRouter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AreWeDoomd.ChatProviders;

public static class ChatProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers every chat provider adapter and its supporting HttpClient.
    /// Each provider is keyed by its <see cref="IChatProvider.Name"/> so callers
    /// resolve one with <c>GetRequiredKeyedService&lt;IChatProvider&gt;("openrouter")</c>.
    /// </summary>
    public static IServiceCollection AddChatProviders(
        this IServiceCollection services,
        IConfiguration configuration,
        bool validateOnStart = true)
    {
        // ===== PROVIDER PLUG-IN POINT ===================================
        // Adding a provider later = one new adapter file (clone
        // GeminiProvider or OpenRouterProvider) + one registration line
        // below. Nothing else in this method or the consuming code needs
        // to change.
        AddGeminiProvider(services, configuration, validateOnStart);
        AddOpenRouterProvider(services, configuration, validateOnStart);
        // ================================================================

        return services;
    }

    private static void AddGeminiProvider(IServiceCollection services, IConfiguration configuration, bool validateOnStart)
    {
        var builder = services
            .AddOptions<GeminiProviderOptions>()
            .Bind(configuration.GetSection(GeminiProviderOptions.SectionName));

        if (validateOnStart)
        {
            builder.ValidateDataAnnotations().ValidateOnStart();
        }

        // Named HttpClient via IHttpClientFactory so a resilience handler can be
        // wrapped around it later (.AddStandardResilienceHandler / Polly) without
        // touching the adapter.
        services.AddHttpClient(GeminiProvider.HttpClientName);

        services.AddKeyedSingleton<IChatProvider, GeminiProvider>(GeminiProvider.ProviderName);
    }

    private static void AddOpenRouterProvider(IServiceCollection services, IConfiguration configuration, bool validateOnStart)
    {
        var builder = services
            .AddOptions<OpenRouterProviderOptions>()
            .Bind(configuration.GetSection(OpenRouterProviderOptions.SectionName));

        if (validateOnStart)
        {
            builder.ValidateDataAnnotations().ValidateOnStart();
        }

        services.AddHttpClient(OpenRouterProvider.HttpClientName);

        services.AddKeyedSingleton<IChatProvider, OpenRouterProvider>(OpenRouterProvider.ProviderName);
    }
}

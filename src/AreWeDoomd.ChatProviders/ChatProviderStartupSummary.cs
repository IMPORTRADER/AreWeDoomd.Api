using AreWeDoomd.ChatProviders.Providers.Gemini;
using AreWeDoomd.ChatProviders.Providers.OpenRouter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.ChatProviders;

/// <summary>
/// Logs which registered chat providers have an API key configured, so a missing
/// key surfaces as a startup log line instead of a runtime exception.
/// </summary>
public static class ChatProviderStartupSummary
{
    private static readonly IReadOnlyDictionary<string, string> ProviderSections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GeminiProvider.ProviderName] = GeminiProviderOptions.SectionName,
            [OpenRouterProvider.ProviderName] = OpenRouterProviderOptions.SectionName
        };

    public static bool IsProviderConfigured(IConfiguration configuration, string providerName)
    {
        return ProviderSections.TryGetValue(providerName, out var section)
            && !string.IsNullOrWhiteSpace(configuration[$"{section}:ApiKey"]);
    }

    /// <summary>Registered provider names with whether an API key is configured.</summary>
    public static IReadOnlyList<(string Name, bool IsConfigured)> ListProviders(IConfiguration configuration)
    {
        return ProviderSections.Keys
            .Select(name => (name, IsProviderConfigured(configuration, name)))
            .ToList();
    }

    /// <summary>
    /// Logs one line per registered provider (Information when its key is present,
    /// Warning when missing), an Error when no provider has a key, and an Error when
    /// the selected provider has no key. Returns whether the selected provider is usable.
    /// </summary>
    public static bool LogSummary(IConfiguration configuration, string selectedProvider, ILogger logger)
    {
        var configuredCount = 0;

        foreach (var (providerName, section) in ProviderSections)
        {
            if (!string.IsNullOrWhiteSpace(configuration[$"{section}:ApiKey"]))
            {
                configuredCount++;
                logger.LogInformation("Chat provider '{Provider}' has an API key configured.", providerName);
            }
            else
            {
                logger.LogWarning("Chat provider '{Provider}' has no API key configured.", providerName);
            }
        }

        if (configuredCount == 0)
        {
            logger.LogError("No chat provider has an API key configured; LLM-dependent features will not work.");
        }

        var selectedConfigured = IsProviderConfigured(configuration, selectedProvider);

        if (selectedConfigured)
        {
            logger.LogInformation("Selected chat provider: '{Provider}'.", selectedProvider);
        }
        else
        {
            logger.LogError(
                "Selected chat provider '{Provider}' has no API key configured (or is not a registered provider).",
                selectedProvider);
        }

        return selectedConfigured;
    }
}

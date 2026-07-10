using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService;

public sealed class KeyedChatProviderResolver(
    IServiceProvider serviceProvider,
    IOptions<AgentServiceOptions> options,
    ILogger<KeyedChatProviderResolver> logger) : IChatProviderResolver
{
    public IChatProvider Resolve(string? providerName)
    {
        string fallback = options.Value.ChatProvider;
        string name = string.IsNullOrWhiteSpace(providerName) ? fallback : providerName.Trim();

        var provider = serviceProvider.GetKeyedService<IChatProvider>(name);
        if (provider is not null)
        {
            return provider;
        }

        if (!string.Equals(name, fallback, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Chat provider '{Provider}' is not registered; falling back to '{Fallback}'.",
                name, fallback);
        }

        return serviceProvider.GetRequiredKeyedService<IChatProvider>(fallback);
    }
}

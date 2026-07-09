using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.ChatProviders;

namespace AreWeDoomd.Api.Common;

public sealed class ChatProviderCatalog(IConfiguration configuration) : IChatProviderCatalog
{
    public IReadOnlyList<ChatProviderInfo> List()
    {
        return ChatProviderStartupSummary.ListProviders(configuration)
            .Select(p => new ChatProviderInfo(p.Name, p.IsConfigured))
            .ToList();
    }

    public bool Exists(string name)
    {
        return List().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}

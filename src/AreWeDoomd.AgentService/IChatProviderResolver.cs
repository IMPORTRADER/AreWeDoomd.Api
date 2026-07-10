using AreWeDoomd.ChatProviders;

namespace AreWeDoomd.AgentService;

public interface IChatProviderResolver
{
    /// <summary>Ayarlardaki provider'ı keyed DI'dan çözer; boş/bilinmeyen ad env varsayılanına düşer.</summary>
    IChatProvider Resolve(string? providerName);
}

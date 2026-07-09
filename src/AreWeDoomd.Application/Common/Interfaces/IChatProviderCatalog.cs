using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IChatProviderCatalog
{
    IReadOnlyList<ChatProviderInfo> List();

    bool Exists(string name);
}

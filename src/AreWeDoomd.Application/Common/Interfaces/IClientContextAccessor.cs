using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IClientContextAccessor
{
    Task<ClientContext> GetCurrentAsync(CancellationToken cancellationToken);
}


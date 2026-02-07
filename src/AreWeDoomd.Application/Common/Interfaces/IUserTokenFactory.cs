using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IUserTokenFactory
{
    Task<AuthToken> CreateAsync(User user, ClientContext clientContext, CancellationToken cancellationToken);
}


using AreWeDoomd.Application.Common.Exceptions;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Infrastructure.Authentication.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Infrastructure.Authentication.ClientTokens;

public sealed class ClientContextAccessor(
    IHttpContextAccessor httpContextAccessor,
    IClientTokenValidator tokenValidator,
    IOptionsMonitor<ClientAuthenticationOptions> optionsMonitor,
    ILogger<ClientContextAccessor> logger) : IClientContextAccessor
{
    private ClientContext? _cached;

    public async Task<ClientContext> GetCurrentAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var httpContext = httpContextAccessor.HttpContext
            ?? throw new ClientAuthenticationException("HTTP context is not available.");

        var options = optionsMonitor.CurrentValue;
        var headerName = options.HeaderName;

        if (!httpContext.Request.Headers.TryGetValue(headerName, out var tokenValue) || string.IsNullOrWhiteSpace(tokenValue))
        {
            logger.LogWarning("Client token header {HeaderName} is missing.", headerName);
            throw new ClientAuthenticationException("Client token is missing.");
        }

        var token = tokenValue.ToString();
        const string bearerPrefix = "Bearer ";
        if (token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token[bearerPrefix.Length..].Trim();
        }

        _cached = await tokenValidator.ValidateAsync(token, cancellationToken);
        return _cached;
    }
}


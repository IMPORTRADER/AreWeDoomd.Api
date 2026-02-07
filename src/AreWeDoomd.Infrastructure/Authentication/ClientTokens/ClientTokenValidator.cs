using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AreWeDoomd.Application.Common.Exceptions;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Infrastructure.Authentication.Jwt;
using AreWeDoomd.Infrastructure.Authentication.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AreWeDoomd.Infrastructure.Authentication.ClientTokens;

public interface IClientTokenValidator
{
    Task<ClientContext> ValidateAsync(string token, CancellationToken cancellationToken);
}

internal sealed class ClientTokenValidator(
    IOptionsMonitor<ClientAuthenticationOptions> optionsMonitor,
    ILogger<ClientTokenValidator> logger)
    : IClientTokenValidator
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public Task<ClientContext> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;

        if (options.Clients.Count == 0)
        {
            throw new ClientAuthenticationException("Client authentication is not configured.");
        }

        foreach (var client in options.Clients)
        {
            try
            {
                using var rsa = RsaKeyLoader.LoadPublicKey(client.PublicKey);
                var parameters = BuildParameters(client, rsa);
                var principal = _tokenHandler.ValidateToken(token, parameters, out var validatedToken);

                var tokenId = principal.FindFirstValue(JwtRegisteredClaimNames.Jti) ?? Guid.NewGuid().ToString();
                var expiresAt = DateTime.SpecifyKind(validatedToken.ValidTo, DateTimeKind.Utc);

                return Task.FromResult(new ClientContext(
                    client.ClientId,
                    client.UserType,
                    client.Issuer,
                    client.Audience,
                    tokenId,
                    new DateTimeOffset(expiresAt)));
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Failed to validate client token for {ClientId}", client.ClientId);
            }
        }

        throw new ClientAuthenticationException("Client token could not be validated.");
    }

    private static TokenValidationParameters BuildParameters(ClientDefinition client, RSA rsa)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = client.Issuer,
            ValidateAudience = true,
            ValidAudience = client.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            ValidAlgorithms = [client.Algorithm],
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }
}


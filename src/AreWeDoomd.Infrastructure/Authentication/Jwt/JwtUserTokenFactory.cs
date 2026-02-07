using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Authentication.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AreWeDoomd.Infrastructure.Authentication.Jwt;

public sealed class JwtUserTokenFactory(
    IOptionsMonitor<UserJwtOptions> optionsMonitor,
    IDateTimeProvider dateTimeProvider) : IUserTokenFactory
{
    public Task<AuthToken> CreateAsync(User user, ClientContext clientContext, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        var now = dateTimeProvider.UtcNow;
        var expiresAt = now.AddMinutes(Math.Max(1, options.AccessTokenLifetimeMinutes));

        using var rsa = RsaKeyLoader.LoadPrivateKey(options.PrivateKey);
        var securityKey = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(securityKey, options.Algorithm);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("client_id", clientContext.ClientId),
            new("user_type", user.UserType.ToString())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        var tokenValue = handler.WriteToken(token);

        return Task.FromResult(new AuthToken(tokenValue, expiresAt));
    }
}


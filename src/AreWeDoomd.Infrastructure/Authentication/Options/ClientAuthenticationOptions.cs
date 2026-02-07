using AreWeDoomd.Domain.Users;
using Microsoft.IdentityModel.Tokens;

namespace AreWeDoomd.Infrastructure.Authentication.Options;

public sealed class ClientAuthenticationOptions
{
    public const string SectionName = "ClientAuthentication";
    public string HeaderName { get; set; } = "X-Client-Assertion";
    public List<ClientDefinition> Clients { get; set; } = [];
}

public sealed class ClientDefinition
{
    public string ClientId { get; set; } = string.Empty;
    public UserType UserType { get; set; } = UserType.Human;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Algorithm { get; set; } = SecurityAlgorithms.RsaSha256;
    public string PublicKey { get; set; } = string.Empty;
}


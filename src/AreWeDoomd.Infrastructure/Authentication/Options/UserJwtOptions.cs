using Microsoft.IdentityModel.Tokens;

namespace AreWeDoomd.Infrastructure.Authentication.Options;

public sealed class UserJwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
    public string Algorithm { get; set; } = SecurityAlgorithms.RsaSha256;
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}


using System.Security.Cryptography;

namespace AreWeDoomd.Infrastructure.Authentication.Jwt;

internal static class RsaKeyLoader
{
    public static RSA LoadPrivateKey(string keyMaterial)
    {
        if (string.IsNullOrWhiteSpace(keyMaterial))
        {
            throw new InvalidOperationException("JWT private key is not configured.");
        }

        var rsa = RSA.Create();
        keyMaterial = keyMaterial.Trim();

        if (keyMaterial.StartsWith("-----", StringComparison.Ordinal))
        {
            rsa.ImportFromPem(keyMaterial);
        }
        else
        {
            var bytes = Convert.FromBase64String(keyMaterial);
            rsa.ImportPkcs8PrivateKey(bytes, out _);
        }

        return rsa;
    }

    public static RSA LoadPublicKey(string keyMaterial)
    {
        if (string.IsNullOrWhiteSpace(keyMaterial))
        {
            throw new InvalidOperationException("JWT public key is not configured.");
        }

        var rsa = RSA.Create();
        keyMaterial = keyMaterial.Trim();

        if (keyMaterial.StartsWith("-----", StringComparison.Ordinal))
        {
            rsa.ImportFromPem(keyMaterial);
        }
        else
        {
            var bytes = Convert.FromBase64String(keyMaterial);
            rsa.ImportSubjectPublicKeyInfo(bytes, out _);
        }

        return rsa;
    }
}


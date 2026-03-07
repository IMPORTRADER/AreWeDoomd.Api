using System.Security.Cryptography;
using AreWeDoomd.Application.Common.Interfaces;

namespace AreWeDoomd.Infrastructure.Common.Services;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const byte Version = 0x01;

    public string Hash(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Value cannot be empty.", nameof(value));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(value, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        var payload = new byte[1 + SaltSize + KeySize];
        payload[0] = Version;
        Buffer.BlockCopy(salt, 0, payload, 1, SaltSize);
        Buffer.BlockCopy(key, 0, payload, 1 + SaltSize, KeySize);

        return Convert.ToBase64String(payload);
    }

    public bool Verify(string hash, string candidate)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (payload.Length != 1 + SaltSize + KeySize)
        {
            return false;
        }

        if (payload[0] != Version)
        {
            return false;
        }

        var salt = new byte[SaltSize];
        Buffer.BlockCopy(payload, 1, salt, 0, SaltSize);
        var storedKey = new byte[KeySize];
        Buffer.BlockCopy(payload, 1 + SaltSize, storedKey, 0, KeySize);

        var computedKey = Rfc2898DeriveBytes.Pbkdf2(candidate, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return CryptographicOperations.FixedTimeEquals(storedKey, computedKey);
    }
}


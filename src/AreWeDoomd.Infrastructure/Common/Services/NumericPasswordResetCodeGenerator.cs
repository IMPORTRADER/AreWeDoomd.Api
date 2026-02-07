using System.Security.Cryptography;
using AreWeDoomd.Application.Common.Interfaces;

namespace AreWeDoomd.Infrastructure.Common.Services;

public sealed class NumericPasswordResetCodeGenerator : IPasswordResetCodeGenerator
{
    public string Generate(int digits)
    {
        var length = Math.Clamp(digits, 4, 12);
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString($"D{length}");
    }
}


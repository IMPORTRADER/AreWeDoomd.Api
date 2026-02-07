namespace AreWeDoomd.Application.Common.Options;

public sealed class PasswordResetOptions
{
    public int CodeLength { get; set; } = 6;
    public int LifetimeMinutes { get; set; } = 15;
}


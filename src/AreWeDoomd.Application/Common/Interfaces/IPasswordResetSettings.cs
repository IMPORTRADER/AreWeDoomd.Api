namespace AreWeDoomd.Application.Common.Interfaces;

public interface IPasswordResetSettings
{
    int CodeLength { get; }
    int LifetimeMinutes { get; }
}


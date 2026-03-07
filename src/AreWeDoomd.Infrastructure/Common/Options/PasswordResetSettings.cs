using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Infrastructure.Common.Options;

public sealed class PasswordResetSettings(IOptionsMonitor<PasswordResetOptions> optionsMonitor) : IPasswordResetSettings
{
    private PasswordResetOptions Current => optionsMonitor.CurrentValue;

    public int CodeLength => Current.CodeLength;
    public int LifetimeMinutes => Current.LifetimeMinutes;
}


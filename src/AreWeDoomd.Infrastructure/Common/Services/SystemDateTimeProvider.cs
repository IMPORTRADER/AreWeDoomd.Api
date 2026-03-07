using AreWeDoomd.Application.Common.Interfaces;

namespace AreWeDoomd.Infrastructure.Common.Services;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}


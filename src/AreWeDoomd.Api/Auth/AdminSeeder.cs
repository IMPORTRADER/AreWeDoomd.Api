using AreWeDoomd.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Api.Auth;

public static class AdminSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(AdminSeeder));

        var dateTimeProvider = scope.ServiceProvider.GetService<IDateTimeProvider>();
        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        await SeedCoreAsync(options.Usernames, users, unitOfWork, now, logger, CancellationToken.None);
    }

    public static async Task<int> SeedCoreAsync(
        IReadOnlyList<string> usernames,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken ct)
    {
        if (usernames.Count == 0)
        {
            return 0;
        }

        int granted = 0;

        foreach (var username in usernames)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                continue;
            }

            var user = await users.GetByUsernameAsync(username, ct);

            if (user is null)
            {
                logger.LogWarning("admin seed: user {Username} not found — skipped", username);
                continue;
            }

            if (user.IsAdmin)
            {
                continue;
            }

            user.GrantAdmin(now);
            await users.UpdateAsync(user, ct);
            granted++;
        }

        if (granted > 0)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        return granted;
    }
}

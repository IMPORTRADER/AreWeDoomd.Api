using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Options;
using AreWeDoomd.Infrastructure.Common.Email;
using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Persistence;
using AreWeDoomd.Infrastructure.Common.Repositories;
using AreWeDoomd.Infrastructure.Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AreWeDoomd.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AreWeDoomdDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("AreWeDoomdSql"),
                sql =>
                {
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AreWeDoomdDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordResetRequestRepository, PasswordResetRequestRepository>();
        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPasswordResetCodeGenerator, NumericPasswordResetCodeGenerator>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IPasswordResetSettings, PasswordResetSettings>();

        services.Configure<PasswordResetOptions>(configuration.GetSection("PasswordReset"));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        return services;
    }
}

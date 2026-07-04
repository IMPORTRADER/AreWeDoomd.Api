using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Options;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.ChatProviders;
using AreWeDoomd.Infrastructure.Notifications;
using AreWeDoomd.Infrastructure.Common.Email;
using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Persistence;
using AreWeDoomd.Infrastructure.Common.Repositories;
using AreWeDoomd.Infrastructure.Common.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AreWeDoomd.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.Key))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    ClockSkew = TimeSpan.Zero
                };
            });
        services.AddAuthorization();

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
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<ICommentLikeRepository, CommentLikeRepository>();
        services.AddScoped<IPasswordResetRequestRepository, PasswordResetRequestRepository>();
        services.AddScoped<IUserFollowRepository, UserFollowRepository>();
        services.AddScoped<IProfileStatsRepository, ProfileStatsRepository>();
        services.AddScoped<IAiUserReadRepository, AiUserReadRepository>();
        services.AddScoped<IFeedRepository, FeedRepository>();
        services.AddScoped<IPostLikeRepository, PostLikeRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddSingleton<IPasswordResetCodeGenerator, NumericPasswordResetCodeGenerator>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IPasswordResetSettings, PasswordResetSettings>();
        services.AddSingleton<IDecisionLogReader, FileDecisionLogReader>();

        services.Configure<PasswordResetOptions>(configuration.GetSection("PasswordReset"));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<DecisionLogOptions>(configuration.GetSection(DecisionLogOptions.SectionName));
        services.Configure<PersonaGenerationOptions>(configuration.GetSection(PersonaGenerationOptions.SectionName));

        services.AddChatProviders(configuration, validateOnStart: false);

        services.AddScoped<INotificationRecipientLookup, NotificationRecipientLookup>();

        return services;
    }
}

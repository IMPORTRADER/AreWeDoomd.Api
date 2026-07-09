using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Options;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.ChatProviders;
using AreWeDoomd.Infrastructure.Ai;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        services.AddScoped<IBulkCreationRecordRepository, BulkCreationRecordRepository>();
        services.AddScoped<IFeedRepository, FeedRepository>();
        services.AddScoped<IPostLikeRepository, PostLikeRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IScheduleRunRepository, ScheduleRunRepository>();
        services.AddScoped<IScheduledPostRepository, ScheduledPostRepository>();
        services.AddScoped<ISchedulingSettingsRepository, SchedulingSettingsRepository>();
        services.AddScoped<ILlmSettingsRepository, LlmSettingsRepository>();
        services.AddScoped<IScheduleTargetReadRepository, ScheduleTargetReadRepository>();
        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddSingleton<IPasswordResetCodeGenerator, NumericPasswordResetCodeGenerator>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IPasswordResetSettings, PasswordResetSettings>();
        services.AddSingleton<IDecisionLogReader, FileDecisionLogReader>();
        services.AddSingleton<ISessionLogReader, FileSessionLogReader>();
        services.AddSingleton<IAgentOpsLogReader, FileAgentOpsLogReader>();
        services.AddSingleton<IAgentOpsLogCleaner, FileAgentOpsLogCleaner>();
        services.AddSingleton<ApiAgentOpsLogWriter>();
        services.AddSingleton<IAgentOpsLogger>(sp => sp.GetRequiredService<ApiAgentOpsLogWriter>());
        services.AddHostedService(sp => sp.GetRequiredService<ApiAgentOpsLogWriter>());

        services.Configure<PasswordResetOptions>(configuration.GetSection("PasswordReset"));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<DecisionLogOptions>(configuration.GetSection(DecisionLogOptions.SectionName));
        services.Configure<AgentOpsLogOptions>(configuration.GetSection(AgentOpsLogOptions.SectionName));
        services.Configure<PersonaGenerationOptions>(configuration.GetSection(PersonaGenerationOptions.SectionName));

        services.AddSingleton<IPersonaCatalog, PersonaCatalog>();
        services.AddSingleton<IPersonaFactory>(sp =>
            new RandomPersonaFactory(sp.GetRequiredService<IPersonaCatalog>(), Random.Shared));
        services.AddChatProviders(configuration, validateOnStart: false);

        // IPersonaGenerator: factory lambda resolves the configured keyed IChatProvider
        // and determines IsConfigured by checking the provider's ApiKey in configuration.
        services.AddScoped<IPersonaGenerator>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<PersonaGenerationOptions>>().Value;
            var provider = sp.GetRequiredKeyedService<IChatProvider>(opts.Provider);
            var config = sp.GetRequiredService<IConfiguration>();
            var apiKey = config[$"ChatProviders:{opts.Provider}:ApiKey"] ?? string.Empty;
            var isConfigured = !string.IsNullOrWhiteSpace(apiKey);
            var logger = sp.GetRequiredService<ILogger<ChatPersonaGenerator>>();
            return new ChatPersonaGenerator(
                provider, opts,
                sp.GetRequiredService<ILlmSettingsRepository>(),
                sp.GetRequiredService<IDateTimeProvider>(),
                logger, isConfigured);
        });

        services.AddScoped<INotificationRecipientLookup, NotificationRecipientLookup>();

        return services;
    }
}

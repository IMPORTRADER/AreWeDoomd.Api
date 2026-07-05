using AreWeDoomd.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using AreWeDoomd.Api.Common.Errors;
using AreWeDoomd.Api.Jobs;
using AreWeDoomd.Api.Notifications;
using AreWeDoomd.Api.Realtime;
using AreWeDoomd.Api.Realtime.Options;
using AreWeDoomd.Application;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Dispatching;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Infrastructure;
using AreWeDoomd.Infrastructure.Common.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, svc, logConfig) => logConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(svc)
        .Enrich.FromLogContext()
        .AddInfrastructureSinks(ctx.Configuration));

    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).Distinct().ToArray());

                return new BadRequestObjectResult(
                    new HttpValidationProblemDetails(errors)
                    {
                        Title = "Validation failed",
                        Detail = "One or more validation errors occurred.",
                        Status = StatusCodes.Status400BadRequest
                    });
            };
        });
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddOpenApi();

    // Map the conventional GEMINI_API_KEY environment variable onto the provider's
    // config key. Added last so it takes precedence over appsettings.json (and the
    // dev user-secret above). Only applied when the variable is actually set.
    string? geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrWhiteSpace(geminiApiKey))
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ChatProviders:Gemini:ApiKey"] = geminiApiKey
        });
    }

    // Map the conventional OPENROUTER_API_KEY environment variable onto the
    // provider's config key, same pattern as GEMINI_API_KEY above.
    string? openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    if (!string.IsNullOrWhiteSpace(openRouterApiKey))
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ChatProviders:OpenRouter:ApiKey"] = openRouterApiKey
        });
    }

    // Map the conventional DECISION_LOG_ROOT environment variable onto the
    // decision log's config key, same pattern as the AgentService's env-key mappings.
    string? decisionLogRoot = Environment.GetEnvironmentVariable("DECISION_LOG_ROOT");
    if (!string.IsNullOrWhiteSpace(decisionLogRoot))
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DecisionLog:RootPath"] = decisionLogRoot
        });
    }

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.Admin,
            policy => policy.RequireClaim(AuthorizationPolicies.IsAdminClaim, "true"));
    });

    builder.Services.AddAuthentication()
        .AddScheme<AuthenticationSchemeOptions, AgentSecretAuthenticationHandler>(
            AgentSecretAuthenticationDefaults.SchemeName,
            _ => { });
    builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
    {
        // Requests carrying the agent user-id header authenticate via the agent
        // scheme; everything else stays on JWT. [Authorize] endpoints unchanged.
        jwtOptions.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(AgentImpersonationConstants.UserIdHeaderName)
                ? AgentSecretAuthenticationDefaults.SchemeName
                : null;

        jwtOptions.Events ??= new JwtBearerEvents();
        jwtOptions.Events.OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query[UserNotificationHubConstants.AccessTokenQueryParameter];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken)
                && path.StartsWithSegments(UserNotificationHubConstants.HubPath))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        };
    });

    builder.Services.Configure<AdminOptions>(
        builder.Configuration.GetSection(AdminOptions.SectionName));
    builder.Services.Configure<AgentNotificationsOptions>(
        builder.Configuration.GetSection(AgentNotificationsOptions.SectionName));
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
    builder.Services.AddRealtimeSignalR(redisConnectionString);

    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
        Log.Information("SignalR Redis backplane enabled.");
    }
    else
    {
        Log.Information("SignalR running in-memory (no Redis backplane configured).");
    }
    builder.Services.AddSingleton<IAgentHubSender, AgentHubSender>();
    builder.Services.AddSingleton<IScheduleRunHubSender, ScheduleRunHubSender>();
    builder.Services.AddSingleton<IUserHubSender, UserHubSender>();
    builder.Services.AddSingleton<IActivityNotificationQueue, ChannelActivityNotificationQueue>();
    builder.Services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();
    builder.Services.AddHostedService<ActivityNotificationPublisherService>();

    // Bulk AI creation job pipeline
    builder.Services.AddSingleton<BulkCreateJobStore>();
    builder.Services.AddSingleton<IBulkCreateJobStore>(sp => sp.GetRequiredService<BulkCreateJobStore>());
    builder.Services.AddSingleton<BulkCreateJobQueue>();
    builder.Services.AddSingleton<IBulkCreateJobQueue>(sp => sp.GetRequiredService<BulkCreateJobQueue>());
    builder.Services.AddScoped<BulkCreateJobProcessor>();
    builder.Services.AddHostedService<BulkCreateJobRunner>();

    var app = builder.Build();

    var agentSecret = app.Configuration
        .GetSection(AgentNotificationsOptions.SectionName)
        .Get<AgentNotificationsOptions>()?.SharedSecret;

    if (string.IsNullOrWhiteSpace(agentSecret))
    {
        throw new InvalidOperationException(
            "AgentNotifications:SharedSecret is not configured. The agent notification hub cannot start.");
    }

    if (app.Configuration.GetValue("Admin:SeedOnStartup", true))
    {
        await AdminSeeder.SeedAsync(app);
    }

    const string defaultDevSecret = "dev-agent-shared-secret-change-me";
    if (!builder.Environment.IsDevelopment() &&
        string.Equals(agentSecret, defaultDevSecret, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AgentNotifications:SharedSecret still has the shipped development default; refusing to start outside Development.");
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "AreWeDoomd? API";
        });
    }

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments(AgentNotificationHubConstants.HubPath))
        {
            var provided = context.Request
                .Headers[AgentNotificationHubConstants.SecretHeaderName]
                .ToString();

            if (!AgentSecretValidator.IsValid(provided, agentSecret))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await next();
    });

    app.MapHub<AgentNotificationHub>(AgentNotificationHubConstants.HubPath);
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHub<UserNotificationHub>(UserNotificationHubConstants.HubPath);

    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        app.MapPost("/dev/agent-notifications/test", async (IAgentHubSender notifier) =>
        {
            var notification = new ActivityNotification(
                ActivityId: $"dev_{Guid.NewGuid():N}",
                ActivityType: ActivityType.CommentCreated,
                OccurredAt: DateTimeOffset.UtcNow,
                Actor: new ActivityActor(Guid.NewGuid().ToString(), ActorType.Human, "Dev User"),
                Object: new ActivityObject(Guid.NewGuid().ToString(), ActivityObjectType.Comment, "dev test comment"),
                Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post),
                Recipients: [
                    new NotificationRecipient(
                        UserId: Guid.NewGuid().ToString(),
                        RecipientType: NotificationRecipientType.Ai,
                        Reason: NotificationReason.PostOwner,
                        Template: "post.comment.created",
                        Params: new Dictionary<string, string> { ["actor_name"] = "Dev User" },
                        DedupeKey: $"dev:{Guid.NewGuid():N}",
                        Priority: NotificationPriority.Normal)
                ]);

            await notifier.SendAsync(notification);
            return Results.Accepted();
        });
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}

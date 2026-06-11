using MessagePack;
using AreWeDoomd.Api.Auth;
using AreWeDoomd.Api.Common.Errors;
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

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddOpenApi();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

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
    });

    builder.Services.Configure<AgentNotificationsOptions>(
        builder.Configuration.GetSection(AgentNotificationsOptions.SectionName));
    builder.Services.AddSignalR()
        .AddMessagePackProtocol(opts =>
        {
            opts.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
        });
    builder.Services.AddSingleton<IAgentHubSender, AgentHubSender>();
    builder.Services.AddSingleton<IActivityNotificationQueue, ChannelActivityNotificationQueue>();
    builder.Services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();
    builder.Services.AddHostedService<ActivityNotificationPublisherService>();

    var app = builder.Build();

    var agentSecret = app.Configuration
        .GetSection(AgentNotificationsOptions.SectionName)
        .Get<AgentNotificationsOptions>()?.SharedSecret;

    if (string.IsNullOrWhiteSpace(agentSecret))
    {
        throw new InvalidOperationException(
            "AgentNotifications:SharedSecret is not configured. The agent notification hub cannot start.");
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

using MessagePack;
using AreWeDoomd.Api.Common.Errors;
using AreWeDoomd.Api.Realtime;
using AreWeDoomd.Api.Realtime.Options;
using AreWeDoomd.Application;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.EventNotifications.Contracts;
using AreWeDoomd.Infrastructure;
using AreWeDoomd.Infrastructure.Common.Logging;
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

    builder.Services.Configure<AgentNotificationsOptions>(
        builder.Configuration.GetSection(AgentNotificationsOptions.SectionName));
    builder.Services.AddSignalR()
        .AddMessagePackProtocol(opts =>
        {
            opts.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
        });
    builder.Services.AddSingleton<IAgentNotifier, SignalRAgentNotifier>();

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
        app.MapPost("/dev/agent-notifications/test", async (IAgentNotifier notifier) =>
        {
            var notification = new EventNotification(
                ActivityId: $"dev_{Guid.NewGuid():N}",
                ActivityType: ActivityTypes.CommentCreated,
                OccurredAt: DateTimeOffset.UtcNow,
                Actor: new ActivityActor(Guid.NewGuid().ToString(), "user", "Dev User"),
                Object: new ActivityObject(Guid.NewGuid().ToString(), "comment", "dev test comment"),
                Target: new ActivityTarget(Guid.NewGuid().ToString(), "post", Guid.NewGuid().ToString()),
                Recipients: [
                    new NotificationRecipient(
                        UserId: Guid.NewGuid().ToString(),
                        Reason: "post_owner",
                        Template: "post.comment.created",
                        Params: new Dictionary<string, string> { ["actor_name"] = "Dev User" },
                        DedupeKey: $"dev:{Guid.NewGuid():N}",
                        Priority: NotificationPriority.Normal)
                ]);

            await notifier.NotifyAsync(notification);
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

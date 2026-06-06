using System.Reflection;
using AreWeDoomd.Application.Common.Behaviors;
using AreWeDoomd.Application.Notifications.Engine;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AreWeDoomd.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddScoped<INotificationEngine, ActivityNotificationEngine>();
        services.AddScoped<IActivityNotificationRule, CommentCreatedNotificationRule>();

        return services;
    }
}

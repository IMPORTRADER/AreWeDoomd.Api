using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AreWeDoomd.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        return services;
    }
}


using AreWeDoomd.Api.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class SignalRRegistrationExtensionsTests
{
    [Fact]
    public void AddRealtimeSignalR_WhenNoRedisConfigured_UsesDefaultHubLifetimeManager()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddRealtimeSignalR(redisConnectionString: null);

        var descriptor = services.Last(d => d.ServiceType == typeof(HubLifetimeManager<>));
        descriptor.ImplementationType.ShouldBe(typeof(DefaultHubLifetimeManager<>));
    }

    [Fact]
    public void AddRealtimeSignalR_WhenWhitespaceRedisConfigured_UsesDefaultHubLifetimeManager()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddRealtimeSignalR(redisConnectionString: "   ");

        var descriptor = services.Last(d => d.ServiceType == typeof(HubLifetimeManager<>));
        descriptor.ImplementationType.ShouldBe(typeof(DefaultHubLifetimeManager<>));
    }

    [Fact]
    public void AddRealtimeSignalR_WhenRedisConfigured_UsesRedisHubLifetimeManager()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // abortConnect=false ensures no eager connection even if a provider were built;
        // this test only inspects descriptors, so nothing connects regardless.
        services.AddRealtimeSignalR("localhost:6379,abortConnect=false");

        var descriptor = services.Last(d => d.ServiceType == typeof(HubLifetimeManager<>));
        descriptor.ImplementationType.ShouldNotBeNull();
        descriptor.ImplementationType!.Name.ShouldStartWith("RedisHubLifetimeManager");
    }
}

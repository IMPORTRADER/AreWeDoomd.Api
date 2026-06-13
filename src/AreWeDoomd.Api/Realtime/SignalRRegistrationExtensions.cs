using MessagePack;
using StackExchange.Redis;

namespace AreWeDoomd.Api.Realtime;

public static class SignalRRegistrationExtensions
{
    // Registers SignalR with the MessagePack protocol. When a Redis connection
    // string is supplied, a StackExchange.Redis backplane is added so hub
    // messages fan out across multiple API instances (horizontal scaling).
    // When the connection string is null/empty, SignalR runs in-memory.
    public static IServiceCollection AddRealtimeSignalR(
        this IServiceCollection services,
        string? redisConnectionString)
    {
        var signalRBuilder = services.AddSignalR()
            .AddMessagePackProtocol(opts =>
            {
                opts.SerializerOptions = MessagePackSerializerOptions.Standard
                    .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
            });

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
            {
                // Isolates this app's SignalR pub/sub channels so a Redis instance
                // shared with other workloads (or other AWD services) does not
                // cross-talk.
                options.Configuration.ChannelPrefix = RedisChannel.Literal("awd:signalr");
            });
        }

        return services;
    }
}

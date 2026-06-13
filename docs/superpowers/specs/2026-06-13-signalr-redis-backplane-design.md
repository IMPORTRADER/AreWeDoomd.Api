# SignalR Redis Backplane — Design

**Date:** 2026-06-13
**Status:** Approved

## Goal

Enable horizontal scaling of the API by adding a Redis backplane to SignalR, so
that hub messages (notifications) fan out across multiple API instances. A client
connected to instance A must receive a message published from instance B.

## Constraints

- The backplane must be **conditional**: enabled only when a Redis connection
  string is configured. Local IDE development (`dotnet run`), the in-memory
  integration tests, and single-instance deployments must continue to work with
  **zero** Redis dependency.
- Match existing repository conventions (config keys, package versions, inline
  `Program.cs` composition style).
- No changes to the hubs themselves or to the AgentService (which is a hub
  *client*, not a server).

## Existing Context

- Two hubs: `AgentNotificationHub` (`/hubs/agent-notifications`) and
  `UserNotificationHub`, registered in `src/AreWeDoomd.Api/Program.cs`.
- SignalR is registered at `Program.cs:71` with the MessagePack protocol.
- `docker-compose.yml` already contains a `redis:7-alpine` service, provisioned
  ahead of use; no app config currently points at it.
- Target framework `net10.0`; ASP.NET Core packages at `10.0.9`.
- Config follows the `ConnectionStrings:AreWeDoomdSql` convention (env var
  `ConnectionStrings__AreWeDoomdSql`).

## Components

### 1. Package reference

Add to `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="10.0.9" />
```

Version chosen to match the other ASP.NET Core packages already in the project.

### 2. Conditional registration (`Program.cs`)

Config key: `ConnectionStrings:Redis` (env var `ConnectionStrings__Redis`),
matching the existing `ConnectionStrings:AreWeDoomdSql` convention.

Read the connection string, build the SignalR builder once (preserving the
existing MessagePack configuration), and chain the backplane only when the
connection string is non-empty:

```csharp
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

var signalRBuilder = builder.Services.AddSignalR()
    .AddMessagePackProtocol(opts =>
    {
        opts.SerializerOptions = MessagePackSerializerOptions.Standard
            .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
    });

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("awd");
    });
    Log.Information("SignalR Redis backplane enabled.");
}
else
{
    Log.Information("SignalR running in-memory (no Redis backplane configured).");
}
```

Notes:
- `ChannelPrefix` isolates this app's pub/sub channels so a Redis instance shared
  with other workloads (e.g. caching) does not cross-talk.
- In StackExchange.Redis 2.x, `ChannelPrefix` is a `RedisChannel`, so it requires
  `RedisChannel.Literal(...)` rather than a bare string. The exact type/usage will
  be confirmed against the package at implementation time.
- The MessagePack configuration is unchanged. The backplane uses its own
  server-to-server serialization, independent of the client wire protocol, so
  there is no conflict.
- `RedisChannel` lives in the `StackExchange.Redis` namespace; the appropriate
  `using` will be added.

### 3. docker-compose wiring

Add to the `api` service `environment` in `docker-compose.yml`, pointing at the
existing `redis` service:

```yaml
ConnectionStrings__Redis: redis:6379
```

Optionally add `redis` to the `api` service's `depends_on`. The `agent-service`
requires no change — it connects to the hub as a client.

### 4. Documentation

Add a note to `.env.example` (and/or the containerization docs) explaining:
set `ConnectionStrings__Redis` to enable the SignalR backplane for multi-instance
deployments; leave it unset for single-instance deployments and local dev.

## Data Flow

1. A message is published on instance A via `IUserHubSender` / `IAgentHubSender`.
2. SignalR's Redis backplane publishes it to the Redis pub/sub channel
   (prefixed `awd`).
3. All API instances subscribed to that channel receive it and deliver to their
   locally connected clients.
4. The client connected to instance B receives the message even though it
   originated on instance A.

## Testing

- **In-memory integration tests** (`tests/AreWeDoomd.IntegrationTests`): the
  `WebApplicationFactory`-based test host does not set `ConnectionStrings:Redis`,
  so the backplane branch is skipped and the hubs run in-memory exactly as today.
  Confirm the test factory does not inherit a Redis connection string from any
  shared configuration source.
- **Local IDE dev**: `appsettings.Development.json` does not set the key; runs
  in-memory.
- **Manual multi-instance verification** (out of scope for automated tests):
  run two API instances against the docker-compose `redis`, connect a client to
  each, and confirm a message published on one is received on the other.

## Out of Scope

- Redis-backed sticky-session / connection store: the backplane handles fan-out;
  SignalR's default reconnect behavior is sufficient.
- Redis resilience / retry policy: consistent with the deliberate "resilience is
  a later phase" stance for the realtime subsystem.
- Any changes to the hub classes or the AgentService client.

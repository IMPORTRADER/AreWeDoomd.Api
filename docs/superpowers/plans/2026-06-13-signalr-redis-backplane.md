# SignalR Redis Backplane Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a conditional Redis backplane to SignalR so the API can scale horizontally — hub messages fan out across instances only when a Redis connection string is configured.

**Architecture:** A small extension method on `IServiceCollection` performs the SignalR registration (MessagePack protocol, unchanged) and chains `AddStackExchangeRedis` only when `ConnectionStrings:Redis` is non-empty. `Program.cs` calls the extension and logs which mode is active. Extracting the logic into an extension method (a small, justified deviation from the inline snippet in the spec) makes the conditional registration unit-testable without a live Redis and satisfies the repo's one-class-per-file convention.

**Tech Stack:** .NET 10, ASP.NET Core SignalR, `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 10.0.9, StackExchange.Redis, xUnit + Shouldly.

---

## File Structure

- **Create** `src/AreWeDoomd.Api/Realtime/SignalRRegistrationExtensions.cs` — extension method `AddRealtimeSignalR(this IServiceCollection, string? redisConnectionString)` that owns SignalR + MessagePack + conditional backplane registration.
- **Modify** `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj` — add the StackExchangeRedis package.
- **Modify** `src/AreWeDoomd.Api/Program.cs:71-76` — replace the inline `AddSignalR().AddMessagePackProtocol(...)` block with a call to the extension + startup log line.
- **Create** `tests/AreWeDoomd.UnitTests/Realtime/SignalRRegistrationExtensionsTests.cs` — DI-wiring unit tests (no live Redis).
- **Modify** `docker-compose.yml:12-16` — add `ConnectionStrings__Redis` to the `api` service and `depends_on: redis`.

---

## Task 1: Add the StackExchange.Redis backplane package

**Files:**
- Modify: `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`

- [ ] **Step 1: Add the package reference**

In `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`, add this line to the first `<ItemGroup>` (the one containing the other `Microsoft.AspNetCore.*` packages), keeping alphabetical-ish ordering next to the other SignalR package:

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="10.0.9" />
```

- [ ] **Step 2: Restore and build to verify the package resolves**

Run: `dotnet build src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`
Expected: Build succeeds (the new package downloads and restores). No code uses it yet.

- [ ] **Step 3: Commit**

```bash
git add src/AreWeDoomd.Api/AreWeDoomd.Api.csproj
git commit -m "build: add SignalR StackExchangeRedis backplane package"
```

---

## Task 2: Conditional registration extension method (TDD)

**Files:**
- Create: `tests/AreWeDoomd.UnitTests/Realtime/SignalRRegistrationExtensionsTests.cs`
- Create: `src/AreWeDoomd.Api/Realtime/SignalRRegistrationExtensions.cs`

> **Why assert by descriptor, not by resolved instance:** `AddSignalR` registers the open generic `HubLifetimeManager<>` via `TryAddSingleton` → `DefaultHubLifetimeManager<>`. `AddStackExchangeRedis` registers it again via `AddSingleton` → `RedisHubLifetimeManager<>`, so when Redis is configured the *last* descriptor for `HubLifetimeManager<>` wins. Inspecting the `IServiceCollection` descriptors checks the effective registration **without** building a provider or opening a Redis connection. `RedisHubLifetimeManager<>` is an internal type in the package, so we assert on its type **name** rather than referencing the type directly.

- [ ] **Step 1: Write the failing test**

Create `tests/AreWeDoomd.UnitTests/Realtime/SignalRRegistrationExtensionsTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~SignalRRegistrationExtensionsTests"`
Expected: FAIL — compile error, `AddRealtimeSignalR` does not exist on `IServiceCollection`.

- [ ] **Step 3: Write the extension method**

Create `src/AreWeDoomd.Api/Realtime/SignalRRegistrationExtensions.cs`:

```csharp
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
                // Isolates this app's pub/sub channels so a Redis instance shared
                // with other workloads does not cross-talk.
                options.Configuration.ChannelPrefix = RedisChannel.Literal("awd");
            });
        }

        return services;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~SignalRRegistrationExtensionsTests"`
Expected: PASS — all 3 tests green.

> If `RedisChannel.Literal` does not resolve, the installed StackExchange.Redis exposes `ChannelPrefix` as a string instead; in that case assign `options.Configuration.ChannelPrefix = "awd";`. Re-run the tests after adjusting.

- [ ] **Step 5: Commit**

```bash
git add src/AreWeDoomd.Api/Realtime/SignalRRegistrationExtensions.cs tests/AreWeDoomd.UnitTests/Realtime/SignalRRegistrationExtensionsTests.cs
git commit -m "feat: add conditional SignalR Redis backplane registration"
```

---

## Task 3: Wire Program.cs to the extension method

**Files:**
- Modify: `src/AreWeDoomd.Api/Program.cs:71-76`

- [ ] **Step 1: Replace the inline SignalR registration**

In `src/AreWeDoomd.Api/Program.cs`, replace this existing block (currently at lines 71-76):

```csharp
    builder.Services.AddSignalR()
        .AddMessagePackProtocol(opts =>
        {
            opts.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
        });
```

with:

```csharp
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
```

The `using AreWeDoomd.Api.Realtime;` directive is already present at the top of `Program.cs`, so no new using is needed. The `MessagePack` using at the top is now only used by the extension method — leave the `using MessagePack;` line in place only if other code in `Program.cs` still references it; if the build warns it's unused, remove `using MessagePack;` from `Program.cs`.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Run the realtime integration tests to confirm in-memory behavior is unchanged**

These tests use `AgentHubTestFactory`, which sets only `ConnectionStrings:AreWeDoomdSql` (never `Redis`), so the in-memory path must still pass.

Run: `dotnet test tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj --filter "FullyQualifiedName~Realtime"`
Expected: PASS for `UserNotificationHubTests` and `AgentNotificationHubTests` (any pre-existing unrelated failures noted in project memory are out of scope — confirm the Realtime hub tests specifically pass).

- [ ] **Step 4: Commit**

```bash
git add src/AreWeDoomd.Api/Program.cs
git commit -m "feat: enable SignalR Redis backplane via configuration in Program.cs"
```

---

## Task 4: Wire docker-compose and document the config key

**Files:**
- Modify: `docker-compose.yml:12-16` (api service `environment`) and `docker-compose.yml:5-26` (api `depends_on`)

- [ ] **Step 1: Point the api service at the existing redis service**

In `docker-compose.yml`, in the `api` service's `environment` block, add a line after `AgentNotifications__SharedSecret`:

```yaml
      # Enables the SignalR Redis backplane for horizontal scaling. Fixed
      # internal address of the compose `redis` service below; no .env value
      # needed. Leave unset (locally / single-instance) to run in-memory.
      ConnectionStrings__Redis: redis:6379
```

- [ ] **Step 2: Add a depends_on so api starts after redis**

In `docker-compose.yml`, add a `depends_on` to the `api` service (it currently has none). Place it directly under the `build:` block, before `ports:`:

```yaml
    depends_on:
      - redis
```

- [ ] **Step 3: Validate the compose file**

Run: `docker compose config`
Expected: Prints the resolved configuration with no errors; the `api` service shows `ConnectionStrings__Redis: redis:6379` and a `redis` dependency.

- [ ] **Step 4: Commit**

```bash
git add docker-compose.yml
git commit -m "chore: wire SignalR Redis backplane in docker-compose"
```

---

## Task 5: Full verification

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build`
Expected: Build succeeds with no errors.

- [ ] **Step 2: Run the unit tests**

Run: `dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`
Expected: PASS, including the 3 new `SignalRRegistrationExtensionsTests`.

- [ ] **Step 3: Run the realtime integration tests**

Run: `dotnet test tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj --filter "FullyQualifiedName~Realtime"`
Expected: PASS — in-memory hub behavior unchanged.

- [ ] **Step 4 (optional, manual multi-instance check — requires Docker):**

Out of scope for automated tests, per the spec. To verify fan-out manually: bring up compose with two `api` replicas against the shared `redis`, connect a SignalR client to each replica, publish a notification, and confirm both clients receive it.

---

## Self-Review Notes (addressed)

- **Spec coverage:** package (Task 1), conditional registration with `ConnectionStrings:Redis` + `ChannelPrefix` + MessagePack preserved (Task 2), startup logging (Task 3), docker-compose wiring (Task 4), in-memory tests unaffected (Tasks 3 & 5). Documentation of the key is covered by the inline compose comment in Task 4 Step 1.
- **Deviation from spec:** the spec showed the conditional registration inline in `Program.cs`; this plan extracts it to `AddRealtimeSignalR` for testability and one-class-per-file compliance. Behavior is identical.
- **Type consistency:** `AddRealtimeSignalR(IServiceCollection, string?)` is defined in Task 2 and called identically in Task 3. `ConnectionStrings:Redis` / env `ConnectionStrings__Redis` is used consistently across Tasks 3 and 4.
- **Fallback noted:** `RedisChannel.Literal` vs string `ChannelPrefix` handled by the note in Task 2 Step 4.

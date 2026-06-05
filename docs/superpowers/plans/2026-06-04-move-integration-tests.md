# Move Integration Tests to Dedicated Project — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `AgentNotificationHubTests` and `AgentHubTestFactory` out of `AreWeDoomd.UnitTests` into a new `AreWeDoomd.IntegrationTests` project.

**Architecture:** Create `tests/AreWeDoomd.IntegrationTests/` as a sibling to `UnitTests`. The new project carries only the packages needed for WebApplicationFactory + SignalR client tests. UnitTests loses those packages since no remaining test uses them.

**Tech Stack:** xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.SignalR.Client`, `Microsoft.AspNetCore.SignalR.Protocols.MessagePack`, Shouldly, .NET 10

---

## File Map

| Action | Path |
|--------|------|
| Create | `tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj` |
| Create | `tests/AreWeDoomd.IntegrationTests/Realtime/AgentHubTestFactory.cs` |
| Create | `tests/AreWeDoomd.IntegrationTests/Realtime/AgentNotificationHubTests.cs` |
| Modify | `AreWeDoomd.Api.slnx` |
| Modify | `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj` |
| Delete | `tests/AreWeDoomd.UnitTests/Realtime/AgentHubTestFactory.cs` |
| Delete | `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationHubTests.cs` |

---

## Task 1: Create the IntegrationTests project file

**Files:**
- Create: `tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj`

- [ ] **Step 1: Create the .csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.8" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Protocols.MessagePack" Version="10.0.8" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="Shouldly" Version="4.2.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AreWeDoomd.Api\AreWeDoomd.Api.csproj" />
    <ProjectReference Include="..\..\src\AreWeDoomd.Application\AreWeDoomd.Application.csproj" />
    <ProjectReference Include="..\..\src\AreWeDoomd.EventNotifications.Contracts\AreWeDoomd.EventNotifications.Contracts.csproj" />
  </ItemGroup>

</Project>
```

---

## Task 2: Move test files to the new project (update namespaces)

**Files:**
- Create: `tests/AreWeDoomd.IntegrationTests/Realtime/AgentHubTestFactory.cs`
- Create: `tests/AreWeDoomd.IntegrationTests/Realtime/AgentNotificationHubTests.cs`
- Delete: `tests/AreWeDoomd.UnitTests/Realtime/AgentHubTestFactory.cs`
- Delete: `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationHubTests.cs`

- [ ] **Step 1: Create AgentHubTestFactory.cs in the new project**

Namespace changes from `AreWeDoomd.UnitTests.Realtime` → `AreWeDoomd.IntegrationTests.Realtime`.

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AreWeDoomd.IntegrationTests.Realtime;

public sealed class AgentHubTestFactory : WebApplicationFactory<Program>
{
    public const string SharedSecret = "integration-test-secret";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseSerilog((_, loggerConfig) =>
            loggerConfig.WriteTo.Console());

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("AgentNotifications:SharedSecret", SharedSecret);
        builder.UseSetting(
            "ConnectionStrings:AreWeDoomdSql",
            "Server=localhost;Database=test;Trusted_Connection=True;TrustServerCertificate=True;");
    }
}
```

- [ ] **Step 2: Create AgentNotificationHubTests.cs in the new project**

```csharp
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.EventNotifications.Contracts;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AreWeDoomd.IntegrationTests.Realtime;

public sealed class AgentNotificationHubTests : IClassFixture<AgentHubTestFactory>
{
    private const string Secret = AgentHubTestFactory.SharedSecret;
    private readonly AgentHubTestFactory _factory;

    public AgentNotificationHubTests(AgentHubTestFactory factory)
    {
        _factory = factory;
    }

    private HubConnection BuildConnection(string secret)
    {
        var server = _factory.Server;
        var hubUri = new Uri(server.BaseAddress, AgentNotificationHubConstants.HubPath.TrimStart('/'));

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Headers.Add(AgentNotificationHubConstants.SecretHeaderName, secret);
            })
            .AddMessagePackProtocol(opts =>
            {
                opts.SerializerOptions = MessagePackSerializerOptions.Standard
                    .WithResolver(ContractlessStandardResolver.Instance);
            })
            .Build();
    }

    [Fact]
    public async Task ReceivesNotification_WhenSecretValid()
    {
        await using var connection = BuildConnection(Secret);

        var tcs = new TaskCompletionSource<EventNotification>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<EventNotification>(
            AgentNotificationHubConstants.ReceiveEventMethod,
            notification => tcs.TrySetResult(notification));

        await connection.StartAsync();

        var notifier = _factory.Services.GetRequiredService<IAgentNotifier>();
        var recipientId = Guid.NewGuid().ToString();
        var sent = new EventNotification(
            ActivityId: "test_act_1",
            ActivityType: ActivityTypes.CommentCreated,
            OccurredAt: DateTimeOffset.UtcNow,
            Actor: new ActivityActor(Guid.NewGuid().ToString(), "user", "TestUser"),
            Object: new ActivityObject(Guid.NewGuid().ToString(), "comment", "test comment"),
            Target: new ActivityTarget(Guid.NewGuid().ToString(), "post", recipientId),
            Recipients: [
                new NotificationRecipient(
                    UserId: recipientId,
                    Reason: "post_owner",
                    Template: "post.comment.created",
                    Params: new Dictionary<string, string> { ["actor_name"] = "TestUser" },
                    DedupeKey: "test:dedupe:1",
                    Priority: NotificationPriority.Normal)
            ]);

        await notifier.NotifyAsync(sent);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(tcs.Task, "notification was not received within timeout");

        var received = await tcs.Task;
        received.ActivityId.ShouldBe(sent.ActivityId);
        received.ActivityType.ShouldBe(sent.ActivityType);
        received.Actor.DisplayName.ShouldBe(sent.Actor.DisplayName);
        received.Target.OwnerId.ShouldBe(sent.Target.OwnerId);
        received.Recipients.Count.ShouldBe(1);
        received.Recipients[0].Priority.ShouldBe(NotificationPriority.Normal);
        received.Recipients[0].Params["actor_name"].ShouldBe("TestUser");
    }

    [Fact]
    public async Task ConnectionRejected_WhenSecretInvalid()
    {
        await using var connection = BuildConnection("wrong-secret");

        await Should.ThrowAsync<Exception>(async () => await connection.StartAsync());
    }
}
```

- [ ] **Step 3: Delete the two files from UnitTests**

Delete:
- `tests/AreWeDoomd.UnitTests/Realtime/AgentHubTestFactory.cs`
- `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationHubTests.cs`

---

## Task 3: Register the new project in the solution

**Files:**
- Modify: `AreWeDoomd.Api.slnx`

- [ ] **Step 1: Add the IntegrationTests project to the `/tests/` folder in the slnx**

Add this line inside the `<Folder Name="/tests/">` block:

```xml
<Project Path="tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj" />
```

Result:
```xml
<Folder Name="/tests/">
  <Project Path="tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj" Id="34772326-2f86-4d5d-9274-15342b459287" />
  <Project Path="tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj" />
</Folder>
```

---

## Task 4: Clean up UnitTests project

Remove SignalR client and MVC testing packages — no remaining unit test uses them.

**Files:**
- Modify: `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`

- [ ] **Step 1: Remove the three packages no longer needed**

Remove these `<PackageReference>` lines:
```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.8" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Protocols.MessagePack" Version="10.0.8" />
```

The remaining UnitTests `.csproj` `<ItemGroup>` for packages:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  <PackageReference Include="Moq" Version="4.20.72" />
  <PackageReference Include="Shouldly" Version="4.2.1" />
  <PackageReference Include="xunit" Version="2.9.3" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

---

## Task 5: Verify

- [ ] **Step 1: Restore and build the solution**

```
dotnet build AreWeDoomd.Api.slnx
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Run unit tests**

```
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj
```

Expected: All tests pass, no compile errors.

- [ ] **Step 3: Run integration tests**

```
dotnet test tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj
```

Expected: Both `ReceivesNotification_WhenSecretValid` and `ConnectionRejected_WhenSecretInvalid` pass.

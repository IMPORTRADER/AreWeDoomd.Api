# EventNotification DTO Yeniden Yapılandırması — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `AgentEventNotification`'ı kaldırıp zengin `EventNotification` formatını (actor/object/target/recipients) getirmek; SignalR wire protokolünü MessagePack'e geçirmek; Application katmanına engine ve dispatcher arayüzlerini eklemek.

**Architecture:** `EventNotifications.Contracts` projesindeki sözleşme tipleri komple yenilenir — 7 yeni tip, 2 eski tip kaldırılır. Hem Api hem AgentService `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` paketini kullanır; JSON naming convention karmaşıklığı ortadan kalkar. Application katmanına `INotificationEngine` ve `INotificationDispatcher` arayüzleri eklenir — implementasyonları sonraki task'a bırakılır.

**Tech Stack:** .NET 10, ASP.NET Core SignalR, `Microsoft.AspNetCore.SignalR.Protocols.MessagePack`, xUnit, Shouldly, `Microsoft.AspNetCore.Mvc.Testing`

**Spec:** `docs/superpowers/specs/2026-06-04-event-notification-dtos-and-engine-design.md`

**Konvansiyonlar (AGENTS.md):** Dosya başına tek sınıf/enum/record; tüm kontrol akışında küme parantezi; `I` öneki interface'lerde; `_camelCase` private alanlar.

---

## File Structure

**`src/AreWeDoomd.EventNotifications.Contracts/`**
- DELETE: `AgentEventNotification.cs`
- DELETE: `AgentEventType.cs`
- MODIFY: `AgentNotificationHubConstants.cs` — sabit adı `ReceiveEventMethod` olarak güncellenir
- CREATE: `EventNotification.cs`
- CREATE: `ActivityActor.cs`
- CREATE: `ActivityObject.cs`
- CREATE: `ActivityTarget.cs`
- CREATE: `NotificationRecipient.cs`
- CREATE: `NotificationPriority.cs`
- CREATE: `ActivityTypes.cs`

**`src/AreWeDoomd.Application/`**
- MODIFY: `Common/Interfaces/IAgentNotifier.cs` — `AgentEventNotification` → `EventNotification`
- CREATE: `Notifications/Engine/ActivityContext.cs`
- CREATE: `Notifications/Engine/INotificationEngine.cs`
- CREATE: `Notifications/Dispatching/INotificationDispatcher.cs`

**`src/AreWeDoomd.Api/`**
- MODIFY: `AreWeDoomd.Api.csproj` — MessagePack paketi eklenir
- MODIFY: `Realtime/IAgentNotificationClient.cs` — `ReceiveAgentEvent` → `ReceiveEvent`, parametre tipi güncellenir
- MODIFY: `Realtime/SignalRAgentNotifier.cs` — `AgentEventNotification` → `EventNotification`, metod çağrısı güncellenir
- MODIFY: `Program.cs` — `AddMessagePackProtocol()` + dev tetikleyici yeni tipler

**`src/AreWeDoomd.AgentService/`**
- MODIFY: `AreWeDoomd.AgentService.csproj` — MessagePack paketi eklenir
- MODIFY: `AgentNotificationListener.cs` — `AddMessagePackProtocol()` + `EventNotification`

**`tests/AreWeDoomd.UnitTests/`**
- MODIFY: `AreWeDoomd.UnitTests.csproj` — MessagePack paketi eklenir
- MODIFY: `Realtime/AgentNotificationContractTests.cs` — yeni tiplere göre güncellenir
- MODIFY: `Realtime/AgentNotificationHubTests.cs` — `AddMessagePackProtocol()` + yeni tipler

---

## Task 1: EventNotifications.Contracts — eski tipler kaldırılır, yeni DTO'lar eklenir

**Files:**
- Delete: `src/AreWeDoomd.EventNotifications.Contracts/AgentEventNotification.cs`
- Delete: `src/AreWeDoomd.EventNotifications.Contracts/AgentEventType.cs`
- Modify: `src/AreWeDoomd.EventNotifications.Contracts/AgentNotificationHubConstants.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/EventNotification.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/ActivityActor.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/ActivityObject.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/ActivityTarget.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/NotificationRecipient.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/NotificationPriority.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/ActivityTypes.cs`

- [ ] **Step 1: Eski sözleşme dosyalarını sil**

Run:
```bash
Remove-Item src/AreWeDoomd.EventNotifications.Contracts/AgentEventNotification.cs
Remove-Item src/AreWeDoomd.EventNotifications.Contracts/AgentEventType.cs
```
Expected: Dosyalar silinir, hata yok.

- [ ] **Step 2: HubConstants'taki method adını güncelle**

`src/AreWeDoomd.EventNotifications.Contracts/AgentNotificationHubConstants.cs` — tüm içerik:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public static class AgentNotificationHubConstants
{
    public const string HubPath = "/hubs/agent-notifications";
    public const string ReceiveEventMethod = "ReceiveEvent";
    public const string SecretHeaderName = "X-Agent-Secret";
}
```

- [ ] **Step 3: EventNotification record'unu oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/EventNotification.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record EventNotification(
    string ActivityId,
    string ActivityType,
    DateTimeOffset OccurredAt,
    ActivityActor Actor,
    ActivityObject Object,
    ActivityTarget Target,
    IReadOnlyList<NotificationRecipient> Recipients);
```

- [ ] **Step 4: ActivityActor record'unu oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/ActivityActor.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record ActivityActor(
    string Id,
    string Type,
    string DisplayName);
```

- [ ] **Step 5: ActivityObject record'unu oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/ActivityObject.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record ActivityObject(
    string Id,
    string Type,
    string? TextPreview);
```

- [ ] **Step 6: ActivityTarget record'unu oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/ActivityTarget.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record ActivityTarget(
    string Id,
    string Type,
    string OwnerId);
```

- [ ] **Step 7: NotificationRecipient record'unu oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/NotificationRecipient.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record NotificationRecipient(
    string UserId,
    string Reason,
    string Template,
    IReadOnlyDictionary<string, string> Params,
    string DedupeKey,
    NotificationPriority Priority);
```

- [ ] **Step 8: NotificationPriority enum'ını oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/NotificationPriority.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public enum NotificationPriority
{
    Normal = 0,
    High = 1,
    Critical = 2
}
```

- [ ] **Step 9: ActivityTypes sabit sınıfını oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/ActivityTypes.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public static class ActivityTypes
{
    public const string CommentCreated = "comment.created";
    public const string PostLiked = "post.liked";
}
```

- [ ] **Step 10: Contracts projesinin derlendiğini doğrula**

Run:
```bash
dotnet build src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj
```
Expected: Build succeeded, 0 Error.

- [ ] **Step 11: Commit**

```bash
git add src/AreWeDoomd.EventNotifications.Contracts
git commit -m "feat: replace AgentEventNotification with rich EventNotification DTO types"
```

---

## Task 2: Application — IAgentNotifier güncelleme + engine/dispatcher arayüzleri

**Files:**
- Modify: `src/AreWeDoomd.Application/Common/Interfaces/IAgentNotifier.cs`
- Create: `src/AreWeDoomd.Application/Notifications/Engine/ActivityContext.cs`
- Create: `src/AreWeDoomd.Application/Notifications/Engine/INotificationEngine.cs`
- Create: `src/AreWeDoomd.Application/Notifications/Dispatching/INotificationDispatcher.cs`

- [ ] **Step 1: IAgentNotifier'ı güncelle**

`src/AreWeDoomd.Application/Common/Interfaces/IAgentNotifier.cs` — tüm içerik:

```csharp
using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAgentNotifier
{
    Task NotifyAsync(EventNotification notification, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: ActivityContext record'unu oluştur**

`src/AreWeDoomd.Application/Notifications/Engine/ActivityContext.cs`:

```csharp
namespace AreWeDoomd.Application.Notifications.Engine;

public sealed record ActivityContext(
    string ActivityType,
    string ActorId,
    string ActorDisplayName,
    string ObjectId,
    string ObjectType,
    string? ObjectTextPreview,
    string TargetId,
    string TargetType,
    string TargetOwnerId,
    DateTimeOffset OccurredAt);
```

- [ ] **Step 3: INotificationEngine arayüzünü oluştur**

`src/AreWeDoomd.Application/Notifications/Engine/INotificationEngine.cs`:

```csharp
using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public interface INotificationEngine
{
    Task<EventNotification> ComputeAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: INotificationDispatcher arayüzünü oluştur**

`src/AreWeDoomd.Application/Notifications/Dispatching/INotificationDispatcher.cs`:

```csharp
using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Dispatching;

public interface INotificationDispatcher
{
    Task DispatchAsync(
        EventNotification notification,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Application'ın derlendiğini doğrula**

Run:
```bash
dotnet build src/AreWeDoomd.Application/AreWeDoomd.Application.csproj
```
Expected: Build succeeded, 0 Error.

- [ ] **Step 6: Commit**

```bash
git add src/AreWeDoomd.Application
git commit -m "feat: update IAgentNotifier and add INotificationEngine/INotificationDispatcher interfaces"
```

---

## Task 3: Api — MessagePack entegrasyonu ve tip güncellemeleri

**Files:**
- Modify: `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`
- Modify: `src/AreWeDoomd.Api/Realtime/IAgentNotificationClient.cs`
- Modify: `src/AreWeDoomd.Api/Realtime/SignalRAgentNotifier.cs`
- Modify: `src/AreWeDoomd.Api/Program.cs`

- [ ] **Step 1: MessagePack paketini ekle**

Run:
```bash
dotnet add src/AreWeDoomd.Api/AreWeDoomd.Api.csproj package Microsoft.AspNetCore.SignalR.Protocols.MessagePack
```
Expected: "Package ... added to the project."

- [ ] **Step 2: IAgentNotificationClient'ı güncelle**

`src/AreWeDoomd.Api/Realtime/IAgentNotificationClient.cs` — tüm içerik:

```csharp
using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Api.Realtime;

public interface IAgentNotificationClient
{
    Task ReceiveEvent(EventNotification notification);
}
```

- [ ] **Step 3: SignalRAgentNotifier'ı güncelle**

`src/AreWeDoomd.Api/Realtime/SignalRAgentNotifier.cs` — tüm içerik:

```csharp
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.EventNotifications.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

public sealed class SignalRAgentNotifier : IAgentNotifier
{
    private readonly IHubContext<AgentNotificationHub, IAgentNotificationClient> _hubContext;

    public SignalRAgentNotifier(IHubContext<AgentNotificationHub, IAgentNotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAsync(EventNotification notification, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.ReceiveEvent(notification);
    }
}
```

- [ ] **Step 4: Program.cs'te AddMessagePackProtocol ekle**

`Program.cs`'te `builder.Services.AddSignalR()` satırını bul ve `.AddMessagePackProtocol()` zincirle:

```csharp
builder.Services.AddSignalR()
    .AddMessagePackProtocol();
```

- [ ] **Step 5: Program.cs'teki dev tetikleyiciyi güncelle**

Dev endpoint bloğunu (`/dev/agent-notifications/test`) tüm içeriğiyle değiştir:

```csharp
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
```

- [ ] **Step 6: Api'nin derlendiğini doğrula**

Run:
```bash
dotnet build src/AreWeDoomd.Api/AreWeDoomd.Api.csproj
```
Expected: Build succeeded, 0 Error.

- [ ] **Step 7: Commit**

```bash
git add src/AreWeDoomd.Api
git commit -m "feat: switch Api SignalR to MessagePack and update to EventNotification types"
```

---

## Task 4: AgentService — MessagePack entegrasyonu ve tip güncellemeleri

**Files:**
- Modify: `src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj`
- Modify: `src/AreWeDoomd.AgentService/AgentNotificationListener.cs`

- [ ] **Step 1: MessagePack paketini ekle**

Run:
```bash
dotnet add src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj package Microsoft.AspNetCore.SignalR.Protocols.MessagePack
```
Expected: "Package ... added to the project."

- [ ] **Step 2: AgentNotificationListener'ı güncelle**

`src/AreWeDoomd.AgentService/AgentNotificationListener.cs` — tüm içerik:

```csharp
using AreWeDoomd.EventNotifications.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService;

public sealed class AgentNotificationListener : BackgroundService
{
    private readonly AgentServiceOptions _options;
    private readonly ILogger<AgentNotificationListener> _logger;
    private HubConnection? _connection;

    public AgentNotificationListener(
        IOptions<AgentServiceOptions> options,
        ILogger<AgentNotificationListener> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl, options =>
            {
                options.Headers.Add(
                    AgentNotificationHubConstants.SecretHeaderName,
                    _options.SharedSecret);
            })
            .AddMessagePackProtocol()
            .WithAutomaticReconnect()
            .Build();

        _connection.On<EventNotification>(
            AgentNotificationHubConstants.ReceiveEventMethod,
            notification =>
            {
                _logger.LogInformation(
                    "Received event {ActivityType} from actor {ActorId} with {RecipientCount} recipient(s)",
                    notification.ActivityType,
                    notification.Actor.Id,
                    notification.Recipients.Count);
            });

        await ConnectWithRetryAsync(stoppingToken);
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connection!.StartAsync(stoppingToken);
                _logger.LogInformation(
                    "Connected to agent notification hub at {HubUrl}",
                    _options.HubUrl);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to connect to agent notification hub, retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
```

- [ ] **Step 3: AgentService'in derlendiğini doğrula**

Run:
```bash
dotnet build src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj
```
Expected: Build succeeded, 0 Error.

- [ ] **Step 4: Commit**

```bash
git add src/AreWeDoomd.AgentService
git commit -m "feat: switch AgentService SignalR listener to MessagePack and EventNotification"
```

---

## Task 5: Testler — yeni tiplere ve MessagePack'e güncelleme

**Files:**
- Modify: `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`
- Modify: `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationContractTests.cs`
- Modify: `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationHubTests.cs`

- [ ] **Step 1: Test projesine MessagePack paketini ekle**

Run:
```bash
dotnet add tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj package Microsoft.AspNetCore.SignalR.Protocols.MessagePack
```
Expected: "Package ... added to the project."

- [ ] **Step 2: AgentNotificationContractTests'i güncelle**

`tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationContractTests.cs` — tüm içerik:

```csharp
using AreWeDoomd.EventNotifications.Contracts;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class AgentNotificationContractTests
{
    [Fact]
    public void EventNotification_Constructs_WithAllFields()
    {
        var actorId = Guid.NewGuid().ToString();
        var targetOwnerId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var notification = new EventNotification(
            ActivityId: "act_1",
            ActivityType: ActivityTypes.CommentCreated,
            OccurredAt: now,
            Actor: new ActivityActor(actorId, "user", "Ali"),
            Object: new ActivityObject(Guid.NewGuid().ToString(), "comment", "Harika!"),
            Target: new ActivityTarget(Guid.NewGuid().ToString(), "post", targetOwnerId),
            Recipients: [
                new NotificationRecipient(
                    UserId: targetOwnerId,
                    Reason: "post_owner",
                    Template: "post.comment.created",
                    Params: new Dictionary<string, string> { ["actor_name"] = "Ali" },
                    DedupeKey: $"comment.created:comment_1:{targetOwnerId}",
                    Priority: NotificationPriority.Normal)
            ]);

        notification.ActivityId.ShouldBe("act_1");
        notification.ActivityType.ShouldBe(ActivityTypes.CommentCreated);
        notification.OccurredAt.ShouldBe(now);
        notification.Actor.Id.ShouldBe(actorId);
        notification.Actor.DisplayName.ShouldBe("Ali");
        notification.Object.TextPreview.ShouldBe("Harika!");
        notification.Target.OwnerId.ShouldBe(targetOwnerId);
        notification.Recipients.Count.ShouldBe(1);
        notification.Recipients[0].Priority.ShouldBe(NotificationPriority.Normal);
        notification.Recipients[0].Params["actor_name"].ShouldBe("Ali");
    }
}
```

- [ ] **Step 3: Contract testini çalıştır, geçtiğini doğrula**

Run:
```bash
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~AgentNotificationContractTests"
```
Expected: PASS (1 test).

- [ ] **Step 4: AgentNotificationHubTests'i güncelle**

`tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationHubTests.cs` — tüm içerik:

```csharp
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.EventNotifications.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class AgentNotificationHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Secret = "integration-test-secret";
    private readonly WebApplicationFactory<Program> _factory;

    public AgentNotificationHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AgentNotifications:SharedSecret", Secret);
            builder.UseSetting(
                "ConnectionStrings:AreWeDoomdSql",
                "Server=localhost;Database=test;Trusted_Connection=True;TrustServerCertificate=True;");
        });
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
            .AddMessagePackProtocol()
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

- [ ] **Step 5: Hub entegrasyon testlerini çalıştır, geçtiğini doğrula**

Run:
```bash
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~AgentNotificationHubTests"
```
Expected: PASS (2 test). Eğer MessagePack `IReadOnlyList` veya `IReadOnlyDictionary` deserialize edemezse test `received.Recipients.Count` assert'inde hata verir — bu durumda `EventNotification` ve `NotificationRecipient` record'larındaki interface türlerini sırasıyla `List<NotificationRecipient>` ve `Dictionary<string, string>` ile değiştir.

- [ ] **Step 6: Tüm test paketinin yeşil olduğunu doğrula**

Run:
```bash
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj
```
Expected: PASS — tüm testler geçer.

- [ ] **Step 7: Commit**

```bash
git add tests/AreWeDoomd.UnitTests
git commit -m "test: update notification tests for EventNotification types and MessagePack"
```

---

## Self-Review

- **Spec kapsamı:**
  - `AgentEventNotification` + `AgentEventType` kaldırıldı ✓ (Task 1 Step 1)
  - `ReceiveAgentEventMethod` → `ReceiveEventMethod` ✓ (Task 1 Step 2)
  - Yeni 7 tip (`EventNotification`, `ActivityActor`, `ActivityObject`, `ActivityTarget`, `NotificationRecipient`, `NotificationPriority`, `ActivityTypes`) ✓ (Task 1 Step 3–9)
  - `IAgentNotifier` → `EventNotification` parametresi ✓ (Task 2 Step 1)
  - `INotificationEngine` + `ActivityContext` + `INotificationDispatcher` ✓ (Task 2 Step 2–4)
  - Api MessagePack ✓ (Task 3)
  - AgentService MessagePack ✓ (Task 4)
  - Testler güncellendi ✓ (Task 5)
- **Kapsam dışı (spec bölüm 7):** Engine implementasyonu, Dispatcher implementasyonu, activity wiring, Human bildirim kanalı — bu plana dahil değil.
- **Tip tutarlılığı:** `AgentNotificationHubConstants.ReceiveEventMethod` her yerde aynı sabit; `EventNotification` her yerde aynı tip; `AddMessagePackProtocol()` server (Task 3) + client (Task 4) + test (Task 5) üçünde de var.

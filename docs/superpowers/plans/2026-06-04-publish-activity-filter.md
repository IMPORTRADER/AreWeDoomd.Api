# PublishActivity Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename `EventNotifications.Contracts` → `ActivityNotifications.Contracts` (proje, namespace, tip), `ActivityTarget.OwnerId`'yi kaldır, ardından `[PublishActivity]` attribute + `ActivityEmissionFilter` + `PassthroughNotificationEngine` altyapısını kur; `CommentsController.CreateComment` üzerinde örnek kullanım göster.

**Architecture:** API action filter'ı (globally registered) her action tamamlandıktan sonra `[PublishActivity]` metadata'sını kontrol eder; varsa route + JWT'den `ActivityContext` kurar ve fire-and-forget olarak `INotificationEngine` → `IAgentNotifier` zincirini çalıştırır. `PassthroughNotificationEngine` gerçek engine implement edilene kadar boş recipient listesiyle minimal `ActivityNotification` üretir.

**Tech Stack:** ASP.NET Core 10, `IAsyncActionFilter`, MediatR (dokunulmaz), MessagePack/SignalR (dokunulmaz), xUnit + Moq + Shouldly

---

## File Map

| Durum | Dosya | Açıklama |
|---|---|---|
| git mv | `src/AreWeDoomd.EventNotifications.Contracts/` → `src/AreWeDoomd.ActivityNotifications.Contracts/` | klasör rename |
| git mv | `.../AreWeDoomd.EventNotifications.Contracts.csproj` → `.../AreWeDoomd.ActivityNotifications.Contracts.csproj` | csproj rename |
| git mv | `.../EventNotification.cs` → `.../ActivityNotification.cs` | tip rename |
| Modify | tüm .cs dosyaları (proje içi) | namespace güncelleme |
| Modify | `AreWeDoomd.Api.slnx` | proje yolu güncelleme |
| Modify | 5 `.csproj` + 7 `.cs` tüketici | using + tip referansı güncelleme |
| Modify | `src/AreWeDoomd.ActivityNotifications.Contracts/ActivityTarget.cs` | `OwnerId` kaldır |
| Modify | `src/AreWeDoomd.Application/Notifications/Engine/ActivityContext.cs` | zaten güncel (TargetOwnerId yok) |
| Create | `src/AreWeDoomd.Api/Filters/PublishActivityAttribute.cs` | marker attribute |
| Create | `src/AreWeDoomd.Api/Filters/ActivityEmissionFilter.cs` | filter logic |
| Create | `src/AreWeDoomd.Infrastructure/Notifications/PassthroughNotificationEngine.cs` | engine stub |
| Modify | `src/AreWeDoomd.Infrastructure/DependencyInjection.cs` | engine DI kaydı |
| Modify | `src/AreWeDoomd.Api/Program.cs` | filter DI kaydı + AddControllers güncelleme |
| Modify | `src/AreWeDoomd.Api/Controllers/CommentsController.cs` | `[PublishActivity]` ekleme |
| Modify | `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationContractTests.cs` | OwnerId kaldırma |
| Modify | `tests/AreWeDoomd.IntegrationTests/Realtime/AgentNotificationHubTests.cs` | OwnerId kaldırma |
| Create | `tests/AreWeDoomd.UnitTests/Notifications/PassthroughNotificationEngineTests.cs` | engine unit test |
| Create | `tests/AreWeDoomd.UnitTests/Notifications/ActivityEmissionFilterTests.cs` | filter unit test |
| Modify | `docs/superpowers/specs/2026-06-04-event-notification-dtos-and-engine-design.md` | güncel durumu yansıt |

---

## Task 1: Rename EventNotifications.Contracts → ActivityNotifications.Contracts

**Files:**
- git mv: `src/AreWeDoomd.EventNotifications.Contracts/` → `src/AreWeDoomd.ActivityNotifications.Contracts/`
- Modify: `AreWeDoomd.Api.slnx`
- Modify: `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`
- Modify: `src/AreWeDoomd.Application/AreWeDoomd.Application.csproj`
- Modify: `src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj`
- Modify: `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`
- Modify: `tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj`
- Modify: tüm .cs dosyaları (namespace + EventNotification tipi)

- [ ] **Step 1: Klasörü ve .csproj'u taşı**

```bash
git mv src/AreWeDoomd.EventNotifications.Contracts src/AreWeDoomd.ActivityNotifications.Contracts
cd src/AreWeDoomd.ActivityNotifications.Contracts
git mv AreWeDoomd.EventNotifications.Contracts.csproj AreWeDoomd.ActivityNotifications.Contracts.csproj
git mv EventNotification.cs ActivityNotification.cs
```

- [ ] **Step 2: `ActivityNotification.cs` içindeki namespace ve tip adını güncelle**

```csharp
// src/AreWeDoomd.ActivityNotifications.Contracts/ActivityNotification.cs
namespace AreWeDoomd.ActivityNotifications.Contracts;

public sealed record ActivityNotification(
    string ActivityId,
    ActivityType ActivityType,
    DateTimeOffset OccurredAt,
    ActivityActor Actor,
    ActivityObject Object,
    ActivityTarget Target,
    List<NotificationRecipient> Recipients);
```

- [ ] **Step 3: Kalan .cs dosyalarının namespace'ini güncelle**

Her dosyada `namespace AreWeDoomd.EventNotifications.Contracts;` → `namespace AreWeDoomd.ActivityNotifications.Contracts;` değiştir.

Dosyalar: `ActorType.cs`, `ActivityActor.cs`, `ActivityObject.cs`, `ActivityObjectType.cs`, `ActivityTarget.cs`, `ActivityTargetType.cs`, `ActivityType.cs`, `AgentNotificationHubConstants.cs`, `NotificationPriority.cs`, `NotificationReason.cs`, `NotificationRecipient.cs`

- [ ] **Step 4: Solution dosyasını güncelle**

`AreWeDoomd.Api.slnx`:
```xml
<Project Path="src/AreWeDoomd.ActivityNotifications.Contracts/AreWeDoomd.ActivityNotifications.Contracts.csproj" />
```

- [ ] **Step 5: .csproj ProjectReference'larını güncelle**

`src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`:
```xml
<ProjectReference Include="..\AreWeDoomd.ActivityNotifications.Contracts\AreWeDoomd.ActivityNotifications.Contracts.csproj" />
```

`src/AreWeDoomd.Application/AreWeDoomd.Application.csproj`:
```xml
<ProjectReference Include="..\AreWeDoomd.ActivityNotifications.Contracts\AreWeDoomd.ActivityNotifications.Contracts.csproj" />
```

`src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj`:
```xml
<ProjectReference Include="..\AreWeDoomd.ActivityNotifications.Contracts\AreWeDoomd.ActivityNotifications.Contracts.csproj" />
```

`tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`:
```xml
<ProjectReference Include="..\..\src\AreWeDoomd.ActivityNotifications.Contracts\AreWeDoomd.ActivityNotifications.Contracts.csproj" />
```

`tests/AreWeDoomd.IntegrationTests/AreWeDoomd.IntegrationTests.csproj`:
```xml
<ProjectReference Include="..\..\src\AreWeDoomd.ActivityNotifications.Contracts\AreWeDoomd.ActivityNotifications.Contracts.csproj" />
```

- [ ] **Step 6: Tüketici .cs dosyalarında using + tip adını güncelle**

`src/AreWeDoomd.Application/Common/Interfaces/IAgentNotifier.cs`:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAgentNotifier
{
    Task NotifyAsync(ActivityNotification notification, CancellationToken cancellationToken = default);
}
```

`src/AreWeDoomd.Application/Notifications/Engine/INotificationEngine.cs`:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public interface INotificationEngine
{
    Task<ActivityNotification> ComputeAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default);
}
```

`src/AreWeDoomd.Application/Notifications/Dispatching/INotificationDispatcher.cs`:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Dispatching;

public interface INotificationDispatcher
{
    Task DispatchAsync(
        ActivityNotification notification,
        CancellationToken cancellationToken = default);
}
```

`src/AreWeDoomd.Application/Notifications/Engine/ActivityContext.cs`:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public sealed record ActivityContext(
    ActivityType ActivityType,
    string ActorId,
    ActorType ActorType,
    string ActorDisplayName,
    string ObjectId,
    ActivityObjectType ObjectType,
    string? ObjectTextPreview,
    string TargetId,
    ActivityTargetType TargetType,
    DateTimeOffset OccurredAt);
```

`src/AreWeDoomd.Api/Realtime/IAgentNotificationClient.cs`:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Api.Realtime;

public interface IAgentNotificationClient
{
    Task ReceiveEvent(ActivityNotification notification);
}
```

`src/AreWeDoomd.Api/Realtime/SignalRAgentNotifier.cs`:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

public sealed class SignalRAgentNotifier : IAgentNotifier
{
    private readonly IHubContext<AgentNotificationHub, IAgentNotificationClient> _hubContext;

    public SignalRAgentNotifier(IHubContext<AgentNotificationHub, IAgentNotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAsync(ActivityNotification notification, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.ReceiveEvent(notification);
    }
}
```

`src/AreWeDoomd.AgentService/AgentNotificationListener.cs` — sadece using güncelle:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
```
(Diğer satırlar değişmez.)

`src/AreWeDoomd.Api/Program.cs` — using güncelle:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
```
ve dev endpoint'te `EventNotification` → `ActivityNotification`:
```csharp
var notification = new ActivityNotification(
    ActivityId: $"dev_{Guid.NewGuid():N}",
    ActivityType: ActivityType.CommentCreated,
    OccurredAt: DateTimeOffset.UtcNow,
    Actor: new ActivityActor(Guid.NewGuid().ToString(), ActorType.Human, "Dev User"),
    Object: new ActivityObject(Guid.NewGuid().ToString(), ActivityObjectType.Comment, "dev test comment"),
    Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post, Guid.NewGuid().ToString()),
    Recipients: [
        new NotificationRecipient(
            UserId: Guid.NewGuid().ToString(),
            Reason: NotificationReason.PostOwner,
            Template: "post.comment.created",
            Params: new Dictionary<string, string> { ["actor_name"] = "Dev User" },
            DedupeKey: $"dev:{Guid.NewGuid():N}",
            Priority: NotificationPriority.Normal)
    ]);
```

`tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationContractTests.cs` — using güncelle:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
```
(Tip referansları zaten enum kullanıyor, `ActivityTypes` static class kaldırıldığından `ActivityType.CommentCreated` zaten güncel.)

`tests/AreWeDoomd.IntegrationTests/Realtime/AgentNotificationHubTests.cs` — using güncelle:
```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
```

- [ ] **Step 7: Build alarak doğrula**

```bash
dotnet build
```
Beklenen: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor: rename EventNotifications.Contracts to ActivityNotifications.Contracts"
```

---

## Task 2: Remove ActivityTarget.OwnerId

**Files:**
- Modify: `src/AreWeDoomd.ActivityNotifications.Contracts/ActivityTarget.cs`
- Modify: `src/AreWeDoomd.Api/Program.cs`
- Modify: `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationContractTests.cs`
- Modify: `tests/AreWeDoomd.IntegrationTests/Realtime/AgentNotificationHubTests.cs`

- [ ] **Step 1: `ActivityTarget.cs`'ten `OwnerId`'yi kaldır**

```csharp
namespace AreWeDoomd.ActivityNotifications.Contracts;

public sealed record ActivityTarget(
    string Id,
    ActivityTargetType Type);
```

- [ ] **Step 2: `Program.cs` dev endpoint'te `ActivityTarget` constructor'ını güncelle**

```csharp
Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post),
```

- [ ] **Step 3: `AgentNotificationContractTests.cs` güncelle**

`Target` satırını güncelle:
```csharp
Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post, targetOwnerId),
```
→
```csharp
Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post),
```

`Target.OwnerId` assertion'ını kaldır:
```csharp
notification.Target.OwnerId.ShouldBe(targetOwnerId);  // ← bu satırı sil
```

`targetOwnerId` değişkeni hâlâ `NotificationRecipient.UserId` için kullanılıyor — değişkeni silme.

- [ ] **Step 4: `AgentNotificationHubTests.cs` güncelle**

`Target` satırını güncelle:
```csharp
Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post, recipientId),
```
→
```csharp
Target: new ActivityTarget(Guid.NewGuid().ToString(), ActivityTargetType.Post),
```

`Target.OwnerId` assertion'ını kaldır:
```csharp
received.Target.OwnerId.ShouldBe(sent.Target.OwnerId);  // ← bu satırı sil
```

- [ ] **Step 5: Build + testleri çalıştır**

```bash
dotnet build && dotnet test
```
Beklenen: tüm testler geçer.

- [ ] **Step 6: Commit**

```bash
git add src/AreWeDoomd.ActivityNotifications.Contracts/ActivityTarget.cs \
        src/AreWeDoomd.Api/Program.cs \
        tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationContractTests.cs \
        tests/AreWeDoomd.IntegrationTests/Realtime/AgentNotificationHubTests.cs
git commit -m "refactor: remove ActivityTarget.OwnerId"
```

---

## Task 3: TDD — PassthroughNotificationEngine

**Files:**
- Create: `tests/AreWeDoomd.UnitTests/Notifications/PassthroughNotificationEngineTests.cs`
- Create: `src/AreWeDoomd.Infrastructure/Notifications/PassthroughNotificationEngine.cs`

- [ ] **Step 1: Test dosyasını oluştur ve failing test yaz**

```csharp
// tests/AreWeDoomd.UnitTests/Notifications/PassthroughNotificationEngineTests.cs
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Infrastructure.Notifications;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class PassthroughNotificationEngineTests
{
    [Fact]
    public async Task ComputeAsync_MapsContextToActivityNotification()
    {
        var engine = new PassthroughNotificationEngine();
        var occurredAt = DateTimeOffset.UtcNow;
        var context = new ActivityContext(
            ActivityType: ActivityType.CommentCreated,
            ActorId: "actor_1",
            ActorType: ActorType.Human,
            ActorDisplayName: "Ali",
            ObjectId: "obj_1",
            ObjectType: ActivityObjectType.Comment,
            ObjectTextPreview: "Harika!",
            TargetId: "target_1",
            TargetType: ActivityTargetType.Post,
            OccurredAt: occurredAt);

        var result = await engine.ComputeAsync(context);

        result.ActivityId.ShouldNotBeNullOrWhiteSpace();
        result.ActivityType.ShouldBe(ActivityType.CommentCreated);
        result.OccurredAt.ShouldBe(occurredAt);
        result.Actor.Id.ShouldBe("actor_1");
        result.Actor.Type.ShouldBe(ActorType.Human);
        result.Actor.DisplayName.ShouldBe("Ali");
        result.Object.Id.ShouldBe("obj_1");
        result.Object.Type.ShouldBe(ActivityObjectType.Comment);
        result.Object.TextPreview.ShouldBe("Harika!");
        result.Target.Id.ShouldBe("target_1");
        result.Target.Type.ShouldBe(ActivityTargetType.Post);
        result.Recipients.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

```bash
dotnet test tests/AreWeDoomd.UnitTests --filter "PassthroughNotificationEngineTests"
```
Beklenen: derleme hatası (`PassthroughNotificationEngine` henüz yok)

- [ ] **Step 3: `PassthroughNotificationEngine`'i implement et**

```csharp
// src/AreWeDoomd.Infrastructure/Notifications/PassthroughNotificationEngine.cs
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Notifications.Engine;

namespace AreWeDoomd.Infrastructure.Notifications;

public sealed class PassthroughNotificationEngine : INotificationEngine
{
    public Task<ActivityNotification> ComputeAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var notification = new ActivityNotification(
            ActivityId: $"act_{Guid.NewGuid():N}",
            ActivityType: context.ActivityType,
            OccurredAt: context.OccurredAt,
            Actor: new ActivityActor(context.ActorId, context.ActorType, context.ActorDisplayName),
            Object: new ActivityObject(context.ObjectId, context.ObjectType, context.ObjectTextPreview),
            Target: new ActivityTarget(context.TargetId, context.TargetType),
            Recipients: []);

        return Task.FromResult(notification);
    }
}
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

```bash
dotnet test tests/AreWeDoomd.UnitTests --filter "PassthroughNotificationEngineTests"
```
Beklenen: `Passed: 1`

- [ ] **Step 5: Commit**

```bash
git add tests/AreWeDoomd.UnitTests/Notifications/PassthroughNotificationEngineTests.cs \
        src/AreWeDoomd.Infrastructure/Notifications/PassthroughNotificationEngine.cs
git commit -m "feat: add PassthroughNotificationEngine stub"
```

---

## Task 4: TDD — PublishActivityAttribute + ActivityEmissionFilter

**Files:**
- Create: `tests/AreWeDoomd.UnitTests/Notifications/ActivityEmissionFilterTests.cs`
- Create: `src/AreWeDoomd.Api/Filters/PublishActivityAttribute.cs`
- Create: `src/AreWeDoomd.Api/Filters/ActivityEmissionFilter.cs`

- [ ] **Step 1: Test dosyasını oluştur ve failing testleri yaz**

```csharp
// tests/AreWeDoomd.UnitTests/Notifications/ActivityEmissionFilterTests.cs
using System.Security.Claims;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Filters;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Engine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class ActivityEmissionFilterTests
{
    private static readonly ActivityNotification StubNotification = new(
        ActivityId: "act_stub",
        ActivityType: ActivityType.CommentCreated,
        OccurredAt: DateTimeOffset.UtcNow,
        Actor: new ActivityActor("u1", ActorType.Human, "Ali"),
        Object: new ActivityObject("", ActivityObjectType.Comment, null),
        Target: new ActivityTarget("p1", ActivityTargetType.Post),
        Recipients: []);

    [Fact]
    public async Task OnActionExecutionAsync_NoAttribute_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: false);
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(
            e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithAttribute_200OK_Dispatches()
    {
        var (engine, notifier, filter) = BuildFilter();
        engine.Setup(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(StubNotification);
        notifier.Setup(n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var executing = BuildExecutingContext(withAttribute: true);
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        engine.Verify(
            e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        notifier.Verify(
            n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ExceptionOccurred_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: true);
        var executed = BuildExecutedContext(executing, statusCode: 200,
            exception: new InvalidOperationException("error"));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(
            e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NonSuccessStatus_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: true);
        var executed = BuildExecutedContext(executing, statusCode: 400);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(
            e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_BuildsActivityContextFromRouteAndClaims()
    {
        var (engine, notifier, filter) = BuildFilter();
        ActivityContext? captured = null;
        engine.Setup(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
              .Callback<ActivityContext, CancellationToken>((ctx, _) => captured = ctx)
              .ReturnsAsync(StubNotification);
        notifier.Setup(n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var executing = BuildExecutingContext(withAttribute: true);
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        captured.ShouldNotBeNull();
        captured!.ActivityType.ShouldBe(ActivityType.CommentCreated);
        captured.ActorId.ShouldBe("user_test");
        captured.ActorDisplayName.ShouldBe("Test User");
        captured.TargetId.ShouldBe("post_abc");
        captured.ObjectId.ShouldBe("");
        captured.ActorType.ShouldBe(ActorType.Human);
        captured.TargetType.ShouldBe(ActivityTargetType.Post);
        captured.ObjectType.ShouldBe(ActivityObjectType.Comment);
    }

    private static (Mock<INotificationEngine> engine, Mock<IAgentNotifier> notifier, ActivityEmissionFilter filter) BuildFilter()
    {
        var engine = new Mock<INotificationEngine>();
        var notifier = new Mock<IAgentNotifier>();
        return (engine, notifier, new ActivityEmissionFilter(engine.Object, notifier.Object));
    }

    private static ActionExecutingContext BuildExecutingContext(bool withAttribute)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["postId"] = "post_abc";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user_test"),
            new Claim(ClaimTypes.Name, "Test User")
        ], "test"));

        var actionDescriptor = new ControllerActionDescriptor
        {
            EndpointMetadata = withAttribute
                ? (IList<object>)
                [
                    new PublishActivityAttribute(
                        ActivityType.CommentCreated,
                        ActorType.Human,
                        ActivityObjectType.Comment, null,
                        ActivityTargetType.Post, "postId")
                ]
                : new List<object>()
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private static ActionExecutedContext BuildExecutedContext(
        ActionExecutingContext executing,
        int statusCode,
        Exception? exception = null)
    {
        var context = new ActionExecutedContext(
            new ActionContext(
                executing.HttpContext,
                executing.RouteData,
                executing.ActionDescriptor),
            new List<IFilterMetadata>(),
            new object())
        {
            Result = new ObjectResult(null) { StatusCode = statusCode },
            Exception = exception,
            ExceptionHandled = exception is null
        };
        return context;
    }
}
```

- [ ] **Step 2: Testlerin başarısız olduğunu doğrula**

```bash
dotnet test tests/AreWeDoomd.UnitTests --filter "ActivityEmissionFilterTests"
```
Beklenen: derleme hatası (`PublishActivityAttribute` ve `ActivityEmissionFilter` henüz yok)

- [ ] **Step 3: `PublishActivityAttribute`'u oluştur**

```csharp
// src/AreWeDoomd.Api/Filters/PublishActivityAttribute.cs
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PublishActivityAttribute(
    ActivityType activityType,
    ActorType actorType,
    ActivityObjectType objectType,
    string? objectIdParam,
    ActivityTargetType targetType,
    string? targetIdParam) : Attribute
{
    public ActivityType ActivityType { get; } = activityType;
    public ActorType ActorType { get; } = actorType;
    public ActivityObjectType ObjectType { get; } = objectType;
    public string? ObjectIdParam { get; } = objectIdParam;
    public ActivityTargetType TargetType { get; } = targetType;
    public string? TargetIdParam { get; } = targetIdParam;
}
```

- [ ] **Step 4: `ActivityEmissionFilter`'ı oluştur**

```csharp
// src/AreWeDoomd.Api/Filters/ActivityEmissionFilter.cs
using System.Security.Claims;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Engine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AreWeDoomd.Api.Filters;

public sealed class ActivityEmissionFilter(
    INotificationEngine engine,
    IAgentNotifier notifier) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        var attribute = executed.ActionDescriptor
            .EndpointMetadata
            .OfType<PublishActivityAttribute>()
            .FirstOrDefault();

        if (attribute is null) return;
        if (executed.Exception is not null) return;
        if (executed.Result is IStatusCodeActionResult { StatusCode: int code } && (code < 200 || code >= 300)) return;

        var actorId = executed.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var actorDisplayName = executed.HttpContext.User.FindFirstValue(ClaimTypes.Name) ?? actorId;

        var routeValues = executed.HttpContext.Request.RouteValues;
        var objectId = attribute.ObjectIdParam is not null
            ? routeValues[attribute.ObjectIdParam]?.ToString() ?? ""
            : "";
        var targetId = attribute.TargetIdParam is not null
            ? routeValues[attribute.TargetIdParam]?.ToString() ?? ""
            : "";

        var activityContext = new ActivityContext(
            ActivityType: attribute.ActivityType,
            ActorId: actorId,
            ActorType: attribute.ActorType,
            ActorDisplayName: actorDisplayName,
            ObjectId: objectId,
            ObjectType: attribute.ObjectType,
            ObjectTextPreview: null,
            TargetId: targetId,
            TargetType: attribute.TargetType,
            OccurredAt: DateTimeOffset.UtcNow);

        _ = Task.Run(async () =>
        {
            var notification = await engine.ComputeAsync(activityContext, CancellationToken.None);
            await notifier.NotifyAsync(notification, CancellationToken.None);
        });
    }
}
```

- [ ] **Step 5: Testlerin geçtiğini doğrula**

```bash
dotnet test tests/AreWeDoomd.UnitTests --filter "ActivityEmissionFilterTests"
```
Beklenen: `Passed: 5`

- [ ] **Step 6: Commit**

```bash
git add tests/AreWeDoomd.UnitTests/Notifications/ActivityEmissionFilterTests.cs \
        src/AreWeDoomd.Api/Filters/PublishActivityAttribute.cs \
        src/AreWeDoomd.Api/Filters/ActivityEmissionFilter.cs
git commit -m "feat: add PublishActivityAttribute and ActivityEmissionFilter"
```

---

## Task 5: DI Wiring + Örnek Endpoint

**Files:**
- Modify: `src/AreWeDoomd.Infrastructure/DependencyInjection.cs`
- Modify: `src/AreWeDoomd.Api/Program.cs`
- Modify: `src/AreWeDoomd.Api/Controllers/CommentsController.cs`

- [ ] **Step 1: `PassthroughNotificationEngine`'i Infrastructure DI'a kaydet**

`src/AreWeDoomd.Infrastructure/DependencyInjection.cs` içinde `return services;` satırından önce ekle:

```csharp
services.AddScoped<INotificationEngine, PassthroughNotificationEngine>();
```

ve `using` ekle:
```csharp
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Infrastructure.Notifications;
```

- [ ] **Step 2: `ActivityEmissionFilter`'ı Program.cs'e kaydet**

`src/AreWeDoomd.Api/Program.cs` içinde `builder.Services.AddControllers();` satırını şu şekilde değiştir:

```csharp
builder.Services.AddScoped<ActivityEmissionFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<ActivityEmissionFilter>();
});
```

ve using ekle:
```csharp
using AreWeDoomd.Api.Filters;
```

- [ ] **Step 3: `CommentsController.CreateComment`'e `[PublishActivity]` ekle**

`src/AreWeDoomd.Api/Controllers/CommentsController.cs` içinde `CreateComment` metoduna:

```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Filters;
```

ve method üzerine:
```csharp
[HttpPost("{postId:guid}/comments")]
[Authorize]
[PublishActivity(
    ActivityType.CommentCreated,
    ActorType.Human,
    ActivityObjectType.Comment, objectIdParam: null,
    ActivityTargetType.Post,   targetIdParam: "postId")]
[ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
```

- [ ] **Step 4: Build + tüm testleri çalıştır**

```bash
dotnet build && dotnet test
```
Beklenen: `Build succeeded. 0 Error(s)` ve tüm testler geçer.

- [ ] **Step 5: Commit**

```bash
git add src/AreWeDoomd.Infrastructure/DependencyInjection.cs \
        src/AreWeDoomd.Api/Program.cs \
        src/AreWeDoomd.Api/Controllers/CommentsController.cs
git commit -m "feat: wire ActivityEmissionFilter and add [PublishActivity] to CreateComment"
```

---

## Task 6: Spec Dokümanlarını Güncelle

**Files:**
- Modify: `docs/superpowers/specs/2026-06-04-event-notification-dtos-and-engine-design.md`

- [ ] **Step 1: Eski spec'e "Superseded" notu ekle**

`docs/superpowers/specs/2026-06-04-event-notification-dtos-and-engine-design.md` dosyasının başına ekle:

```markdown
> **Not:** Bu doküman kısmen geçerliliğini yitirmiştir. Güncel kararlar için bkz:
> - `docs/superpowers/specs/2026-06-04-publish-activity-filter-design.md`
>
> Değişenler: `EventNotification` → `ActivityNotification`, `ActivityTypes` static class → `ActivityType` enum,
> `string Type` alanları → enum tipleri, `ActivityTarget.OwnerId` kaldırıldı, `ActivityContext.TargetOwnerId` kaldırıldı.
```

- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/specs/2026-06-04-event-notification-dtos-and-engine-design.md
git commit -m "docs: mark old event notification spec as superseded"
```

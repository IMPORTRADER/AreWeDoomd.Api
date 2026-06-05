# PublishActivity İyileştirmesi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `[PublishActivity]` attribute'unu sadeleştirip, actor tipini JWT Role claim'inden türeten ve yeni yaratılan nesnenin id/tip/önizlemesini response body'den yakalayan bir akış kurmak.

**Architecture:** Response DTO'ları `IActivityObjectCarrier` marker interface'ini explicit implement eder; `ActivityPublishingFilter` (eski `ActivityEmissionFilter`) bu carrier'dan object bilgisini, JWT `ClaimTypes.Role` claim'inden actor tipini okur. Geçersiz/eksik role'de error loglar ve aktiviteyi atlar. Attribute yalnızca `ActivityType`, `ActivityTargetType`, `targetIdParam` taşır.

**Tech Stack:** .NET 10, ASP.NET Core MVC action filters, xUnit + Moq + Shouldly.

> **Commit notu:** Bu repoda commit'ler manuel atılır. Plandaki "Checkpoint" adımlarında `git commit` ÇALIŞTIRMA; sadece build + testlerin yeşil olduğunu doğrula ve kullanıcının manuel commit'i için dur.

**Referans spec:** `docs/superpowers/specs/2026-06-05-publish-activity-improvement-design.md`

---

## File Structure

| Dosya | Sorumluluk | İşlem |
|---|---|---|
| `src/AreWeDoomd.Api/Filters/IActivityObjectCarrier.cs` | Response DTO'nun aktivite-object kimliğini taşıyan marker interface | Create |
| `src/AreWeDoomd.Api/Contracts/Comments/CommentResponse.cs` | Yorum response DTO'su; carrier'ı explicit implement eder | Modify |
| `src/AreWeDoomd.Api/Filters/ActivityPublishingFilter.cs` | Action sonrası `ActivityContext` kurup notification yayar | Create (rename) |
| `src/AreWeDoomd.Api/Filters/ActivityEmissionFilter.cs` | Eski filter | Delete (rename sonrası) |
| `src/AreWeDoomd.Api/Filters/PublishActivityAttribute.cs` | Endpoint işaretleme attribute'u | Modify |
| `src/AreWeDoomd.Api/Controllers/CommentsController.cs` | `[PublishActivity]` kullanımı | Modify |
| `src/AreWeDoomd.Api/Program.cs` | Filter'ın DI kaydı | Modify |
| `tests/AreWeDoomd.UnitTests/Contracts/CommentResponseTests.cs` | Carrier implementasyon testi | Create |
| `tests/AreWeDoomd.UnitTests/Notifications/ActivityPublishingFilterTests.cs` | Filter davranış testleri | Create (rename) |
| `tests/AreWeDoomd.UnitTests/Notifications/ActivityEmissionFilterTests.cs` | Eski filter testleri | Delete (rename sonrası) |

---

## Task 1: `IActivityObjectCarrier` + `CommentResponse` implementasyonu

Additive değişiklik — mevcut derleme bozulmaz. Önce carrier'ın `CommentResponse` üzerindeki davranışını test ederiz.

**Files:**
- Create: `src/AreWeDoomd.Api/Filters/IActivityObjectCarrier.cs`
- Modify: `src/AreWeDoomd.Api/Contracts/Comments/CommentResponse.cs`
- Test: `tests/AreWeDoomd.UnitTests/Contracts/CommentResponseTests.cs`

- [ ] **Step 1: Carrier interface'ini oluştur**

`src/AreWeDoomd.Api/Filters/IActivityObjectCarrier.cs`:

```csharp
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Api.Filters;

/// <summary>
/// Bir response DTO'sunun, ürettiği/temsil ettiği aktivite nesnesinin
/// kimliğini taşıdığını belirtir. ActivityPublishingFilter bu bilgiyi
/// response body'den okur.
/// </summary>
public interface IActivityObjectCarrier
{
    string ActivityObjectId { get; }
    ActivityObjectType ActivityObjectType { get; }
    string? ActivityObjectTextPreview => null;
}
```

- [ ] **Step 2: Failing test'i yaz**

`tests/AreWeDoomd.UnitTests/Contracts/CommentResponseTests.cs`:

```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Filters;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Contracts;

public sealed class CommentResponseTests
{
    [Fact]
    public void CommentResponse_ExposesActivityObjectIdentity()
    {
        var id = Guid.NewGuid();
        var response = new CommentResponse(
            Id: id,
            PostId: Guid.NewGuid(),
            Author: new PostAuthor(Guid.NewGuid(), "ali", "Human", null),
            Content: "merhaba dünya",
            LikeCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);

        IActivityObjectCarrier carrier = response;

        carrier.ActivityObjectId.ShouldBe(id.ToString());
        carrier.ActivityObjectType.ShouldBe(ActivityObjectType.Comment);
        carrier.ActivityObjectTextPreview.ShouldBe("merhaba dünya");
    }
}
```

- [ ] **Step 3: Test'in derlenmediğini/başarısız olduğunu doğrula**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~CommentResponseTests"`
Expected: Derleme hatası — `CommentResponse`, `IActivityObjectCarrier`'a cast edilemez (henüz implement etmiyor).

- [ ] **Step 4: `CommentResponse`'ı carrier'ı implement edecek şekilde güncelle**

`src/AreWeDoomd.Api/Contracts/Comments/CommentResponse.cs`:

```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Filters;

namespace AreWeDoomd.Api.Contracts.Comments;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    PostAuthor Author,
    string Content,
    int LikeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
    : IActivityObjectCarrier
{
    string IActivityObjectCarrier.ActivityObjectId => Id.ToString();
    ActivityObjectType IActivityObjectCarrier.ActivityObjectType => ActivityObjectType.Comment;
    string? IActivityObjectCarrier.ActivityObjectTextPreview => Content;
}
```

- [ ] **Step 5: Test'in geçtiğini doğrula**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~CommentResponseTests"`
Expected: PASS (1 test).

- [ ] **Step 6: Checkpoint**

Run: `dotnet build`
Expected: Çözüm derlenir. Build + test yeşil → kullanıcı manuel commit eder (git commit ÇALIŞTIRMA).

---

## Task 2: Filter'ı yeniden adlandır + yeniden yaz

Filter `ActivityEmissionFilter` → `ActivityPublishingFilter` olur; `ILogger` enjekte edilir; actor tipi Role claim'inden türetilir (geçersizse logla + atla), object bilgisi response body'deki carrier'dan okunur. **Bu adımda attribute hâlâ eski 6 parametreli imzasını taşır** — filter `actorType`/`objectType`/`objectIdParam` alanlarını artık okumaz. Program DI kaydı ve test dosyası yeni isimle güncellenir.

**Files:**
- Create: `src/AreWeDoomd.Api/Filters/ActivityPublishingFilter.cs`
- Delete: `src/AreWeDoomd.Api/Filters/ActivityEmissionFilter.cs`
- Modify: `src/AreWeDoomd.Api/Program.cs:28-32`
- Create: `tests/AreWeDoomd.UnitTests/Notifications/ActivityPublishingFilterTests.cs`
- Delete: `tests/AreWeDoomd.UnitTests/Notifications/ActivityEmissionFilterTests.cs`

- [ ] **Step 1: Yeni test dosyasını yaz (failing)**

Eski `ActivityEmissionFilterTests.cs` dosyasını sil ve yerine `tests/AreWeDoomd.UnitTests/Notifications/ActivityPublishingFilterTests.cs` oluştur:

```csharp
using System.Security.Claims;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Filters;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class ActivityPublishingFilterTests
{
    private static readonly ActivityNotification StubNotification = new(
        ActivityId: "act_stub",
        ActivityType: ActivityType.CommentCreated,
        OccurredAt: DateTimeOffset.UtcNow,
        Actor: new ActivityActor("u1", ActorType.Human, "Ali"),
        Object: new ActivityObject("c1", ActivityObjectType.Comment, "merhaba"),
        Target: new ActivityTarget("p1", ActivityTargetType.Post),
        Recipients: []);

    [Fact]
    public async Task NoAttribute_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: false, role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WithAttribute_200OK_HumanRole_Dispatches()
    {
        var (engine, notifier, filter) = BuildFilter();
        SetupDispatch(engine, notifier);

        var executing = BuildExecutingContext(withAttribute: true, role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExceptionOccurred_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: true, role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 200,
            exception: new InvalidOperationException("error"));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NonSuccessStatus_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: true, role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 400);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AiRole_ProducesAgentActorType()
    {
        var (engine, notifier, filter) = BuildFilter();
        var captured = SetupCapture(engine, notifier);

        var executing = BuildExecutingContext(withAttribute: true, role: nameof(UserType.Ai));
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        captured.Value.ShouldNotBeNull();
        captured.Value!.ActorType.ShouldBe(ActorType.Agent);
    }

    [Fact]
    public async Task InvalidRole_LogsErrorAndSkips()
    {
        var engine = new Mock<INotificationEngine>();
        var notifier = new Mock<IAgentNotifier>();
        var logger = new Mock<ILogger<ActivityPublishingFilter>>();
        var filter = new ActivityPublishingFilter(engine.Object, notifier.Object, logger.Object);

        var executing = BuildExecutingContext(withAttribute: true, role: nameof(UserType.Unknown));
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MissingRoleClaim_LogsErrorAndSkips()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: true, role: null);
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildsContextFromCarrierBodyAndClaims()
    {
        var (engine, notifier, filter) = BuildFilter();
        var captured = SetupCapture(engine, notifier);

        var commentId = Guid.NewGuid();
        var executing = BuildExecutingContext(withAttribute: true, role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 200,
            body: BuildCommentResponse(commentId, "selam"));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        captured.Value.ShouldNotBeNull();
        var ctx = captured.Value!;
        ctx.ActivityType.ShouldBe(ActivityType.CommentCreated);
        ctx.ActorId.ShouldBe("user_test");
        ctx.ActorDisplayName.ShouldBe("Test User");
        ctx.ActorType.ShouldBe(ActorType.Human);
        ctx.TargetId.ShouldBe("post_abc");
        ctx.TargetType.ShouldBe(ActivityTargetType.Post);
        ctx.ObjectId.ShouldBe(commentId.ToString());
        ctx.ObjectType.ShouldBe(ActivityObjectType.Comment);
        ctx.ObjectTextPreview.ShouldBe("selam");
    }

    private static void SetupDispatch(Mock<INotificationEngine> engine, Mock<IAgentNotifier> notifier)
    {
        engine.Setup(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(StubNotification);
        notifier.Setup(n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
    }

    private static StrongBox<ActivityContext?> SetupCapture(Mock<INotificationEngine> engine, Mock<IAgentNotifier> notifier)
    {
        var box = new StrongBox<ActivityContext?>(null);
        engine.Setup(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
              .Callback<ActivityContext, CancellationToken>((ctx, _) => box.Value = ctx)
              .ReturnsAsync(StubNotification);
        notifier.Setup(n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        return box;
    }

    private static CommentResponse BuildCommentResponse(Guid? id = null, string content = "merhaba")
        => new(
            Id: id ?? Guid.NewGuid(),
            PostId: Guid.NewGuid(),
            Author: new PostAuthor(Guid.NewGuid(), "ali", "Human", null),
            Content: content,
            LikeCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);

    private static (Mock<INotificationEngine> engine, Mock<IAgentNotifier> notifier, ActivityPublishingFilter filter) BuildFilter()
    {
        var engine = new Mock<INotificationEngine>();
        var notifier = new Mock<IAgentNotifier>();
        var filter = new ActivityPublishingFilter(
            engine.Object,
            notifier.Object,
            NullLogger<ActivityPublishingFilter>.Instance);
        return (engine, notifier, filter);
    }

    private static ActionExecutingContext BuildExecutingContext(bool withAttribute, string? role)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["postId"] = "post_abc";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user_test"),
            new(ClaimTypes.Name, "Test User")
        };
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

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
        object? body = null,
        Exception? exception = null)
    {
        return new ActionExecutedContext(
            new ActionContext(executing.HttpContext, executing.RouteData, executing.ActionDescriptor),
            new List<IFilterMetadata>(),
            new object())
        {
            Result = new ObjectResult(body) { StatusCode = statusCode },
            Exception = exception,
            ExceptionHandled = exception is null
        };
    }
}
```

> Not: `StrongBox<T>` için `using System.Runtime.CompilerServices;`, `ILogger`/`LogLevel` için `using Microsoft.Extensions.Logging;` gerekir. Bunları dosyanın başına ekle.

- [ ] **Step 2: Test'in derlenmediğini doğrula**

Run: `dotnet build tests/AreWeDoomd.UnitTests`
Expected: Derleme hatası — `ActivityPublishingFilter` tipi henüz yok.

- [ ] **Step 3: Yeni filter'ı oluştur**

Eski `src/AreWeDoomd.Api/Filters/ActivityEmissionFilter.cs` dosyasını sil ve `src/AreWeDoomd.Api/Filters/ActivityPublishingFilter.cs` oluştur:

```csharp
using System.Security.Claims;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AreWeDoomd.Api.Filters;

public sealed class ActivityPublishingFilter(
    INotificationEngine engine,
    IAgentNotifier notifier,
    ILogger<ActivityPublishingFilter> logger) : IAsyncActionFilter
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

        var user = executed.HttpContext.User;

        // Actor tipi — JWT Role claim'inden; güvenilmezse logla ve aktiviteyi atla.
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (role != nameof(UserType.Ai) && role != nameof(UserType.Human))
        {
            logger.LogError("PublishActivity: geçersiz/eksik role claim '{Role}', aktivite atlandı.", role);
            return;
        }
        var actorType = role == nameof(UserType.Ai) ? ActorType.Agent : ActorType.Human;

        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var actorDisplayName = user.FindFirstValue(ClaimTypes.Name) ?? actorId;

        // Object — response body'deki carrier'dan (id + tip + önizleme).
        var objectId = "";
        ActivityObjectType objectType = default;
        string? objectTextPreview = null;
        if (executed.Result is ObjectResult { Value: IActivityObjectCarrier carrier })
        {
            objectId = carrier.ActivityObjectId;
            objectType = carrier.ActivityObjectType;
            objectTextPreview = carrier.ActivityObjectTextPreview;
        }

        // Target — route value'dan.
        var routeValues = executed.HttpContext.Request.RouteValues;
        var targetId = attribute.TargetIdParam is not null
            ? routeValues[attribute.TargetIdParam]?.ToString() ?? ""
            : "";

        var activityContext = new ActivityContext(
            ActivityType: attribute.ActivityType,
            ActorId: actorId,
            ActorType: actorType,
            ActorDisplayName: actorDisplayName,
            ObjectId: objectId,
            ObjectType: objectType,
            ObjectTextPreview: objectTextPreview,
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

> Not: Bu adımda `attribute.TargetType` ve `attribute.TargetIdParam` hâlâ eski attribute'tan okunuyor; `attribute.ActorType`/`ObjectType`/`ObjectIdParam` artık okunmuyor (Task 3'te attribute'tan kalkacak).

- [ ] **Step 4: Program.cs DI kaydını güncelle**

`src/AreWeDoomd.Api/Program.cs:28-32` — `ActivityEmissionFilter` → `ActivityPublishingFilter`:

```csharp
    builder.Services.AddScoped<ActivityPublishingFilter>();
    builder.Services.AddControllers(options =>
    {
        options.Filters.AddService<ActivityPublishingFilter>();
    });
```

- [ ] **Step 5: Testleri çalıştır**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~ActivityPublishingFilterTests"`
Expected: PASS (8 test) — Human dispatch, Ai→Agent, invalid/missing role skip + error log, carrier body'den context, exception/non-2xx/no-attribute skip.

- [ ] **Step 6: Checkpoint**

Run: `dotnet build && dotnet test tests/AreWeDoomd.UnitTests`
Expected: Tüm çözüm derlenir, tüm testler yeşil. → kullanıcı manuel commit eder (git commit ÇALIŞTIRMA).

---

## Task 3: Attribute'u sadeleştir

`PublishActivityAttribute`'tan `actorType`, `objectType`, `objectIdParam` kaldırılır. Kullanım yerleri (controller + test'in attribute construction'ı) 3 parametreli yeni imzaya güncellenir.

**Files:**
- Modify: `src/AreWeDoomd.Api/Filters/PublishActivityAttribute.cs`
- Modify: `src/AreWeDoomd.Api/Controllers/CommentsController.cs:24`
- Modify: `tests/AreWeDoomd.UnitTests/Notifications/ActivityPublishingFilterTests.cs` (BuildExecutingContext)

- [ ] **Step 1: Attribute'u sadeleştir**

`src/AreWeDoomd.Api/Filters/PublishActivityAttribute.cs`:

```csharp
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PublishActivityAttribute(
    ActivityType activityType,
    ActivityTargetType targetType,
    string? targetIdParam) : Attribute
{
    public ActivityType ActivityType { get; } = activityType;
    public ActivityTargetType TargetType { get; } = targetType;
    public string? TargetIdParam { get; } = targetIdParam;
}
```

- [ ] **Step 2: Build'in kırıldığını doğrula**

Run: `dotnet build`
Expected: Derleme hatası — `CommentsController` ve test, 6 parametreli eski constructor'ı çağırıyor.

- [ ] **Step 3: Controller kullanımını güncelle**

`src/AreWeDoomd.Api/Controllers/CommentsController.cs:24`:

```csharp
    [PublishActivity(ActivityType.CommentCreated, ActivityTargetType.Post, targetIdParam: "postId")]
```

- [ ] **Step 4: Test'teki attribute construction'ı güncelle**

`tests/AreWeDoomd.UnitTests/Notifications/ActivityPublishingFilterTests.cs` — `BuildExecutingContext` içindeki `EndpointMetadata` bloğunu yeni imzaya çek:

```csharp
            EndpointMetadata = withAttribute
                ? (IList<object>)
                [
                    new PublishActivityAttribute(
                        ActivityType.CommentCreated,
                        ActivityTargetType.Post,
                        targetIdParam: "postId")
                ]
                : new List<object>()
```

- [ ] **Step 5: Build + testleri çalıştır**

Run: `dotnet build && dotnet test tests/AreWeDoomd.UnitTests`
Expected: Derleme başarılı, tüm testler yeşil.

- [ ] **Step 6: Checkpoint**

Build + test yeşil → kullanıcı manuel commit eder (git commit ÇALIŞTIRMA).

---

## Self-Review Notu

- **Spec kapsamı:** Actor claim'den türetme (Task 2), geçersiz role'de logla+atla (Task 2), object body'den carrier ile (Task 1+2), `ObjectTextPreview` dolumu (Task 1), filter rename (Task 2), attribute sadeleşmesi (Task 3) — hepsi karşılanıyor.
- **Kapsam dışı (spec ile uyumlu):** `ActivityType`↔`ObjectType`/`TargetType` eşlemesi explicit kalıyor; `targetIdParam` string kalıyor; carrier'sız endpoint'te `objectType = default` davranışı bilinçli bırakıldı.
- **Tip tutarlılığı:** `IActivityObjectCarrier` üyeleri (`ActivityObjectId`, `ActivityObjectType`, `ActivityObjectTextPreview`) Task 1, 2 boyunca aynı; filter constructor imzası (`engine, notifier, logger`) test ve Program kaydıyla tutarlı.

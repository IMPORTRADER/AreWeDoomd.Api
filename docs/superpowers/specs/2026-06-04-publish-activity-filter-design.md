# PublishActivity Filter Tasarımı

> Tarih: 2026-06-04  
> Durum: Onaylandı  
> Kapsam: Naming stabilizasyonu + API action filter altyapısı

---

## 1. Amaç

API endpoint'lerini `[PublishActivity]` attribute'u ile işaretleyerek kullanıcı etkileşimlerinden otomatik `ActivityNotification` üretmek; bu notification'ı fire-and-forget olarak AgentService'e iletmek. Bu dokümanda ayrıca tüm codebase'de "Activity" terminolojisine geçiş kararları yer alır.

---

## 2. Kararlar

| Konu | Karar | Gerekçe |
|---|---|---|
| Terminoloji | Her yerde **Activity** prefix'i | Codebase zaten `ActivityContext`, `ActivityActor` vb. kullanıyor; `EventNotification` tek istisnaydı |
| Tip rename | `EventNotification` → `ActivityNotification` | Terminoloji tutarlılığı |
| Proje rename | `AreWeDoomd.EventNotifications.Contracts` → `AreWeDoomd.ActivityNotifications.Contracts` | Tam tutarlılık; proje adı da namespace'i yansıtmalı |
| `TargetOwnerId` | `ActivityContext`'ten kaldırılır | Engine implement edildiğinde `TargetId` üzerinden DB'den bulunur |
| Filter tetikleme | Action başarıyla tamamlandıktan sonra (2xx) | Başarısız istekler notification üretmemeli |
| Dispatch | Fire-and-forget (`_ = Task.Run(...)`) | HTTP yanıt süresi etkilenmemeli |
| Engine stub | `PassthroughNotificationEngine` (Infrastructure) | Recipient listesi boş, `INotificationEngine` implement edilince kaldırılır |
| Örnek endpoint | `CommentsController.CreateComment` | Hem route param (postId) hem de actor (JWT) kullanımını gösterir |

---

## 3. Naming Stabilizasyonu

### 3.1 Etkilenen dosyalar

| Değişen şey | Önce | Sonra |
|---|---|---|
| Proje adı / klasör | `AreWeDoomd.EventNotifications.Contracts` | `AreWeDoomd.ActivityNotifications.Contracts` |
| Namespace | `AreWeDoomd.EventNotifications.Contracts` | `AreWeDoomd.ActivityNotifications.Contracts` |
| Tip adı | `EventNotification` | `ActivityNotification` |
| Tüm `using` direktifleri | güncellenir | — |
| Solution + `.csproj` referansları | güncellenir | — |

### 3.2 `ActivityContext` — `TargetOwnerId` kaldırılması

```csharp
// Önce
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
    string TargetOwnerId,       // ← kaldırıldı
    DateTimeOffset OccurredAt);

// Sonra
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

---

## 4. `[PublishActivity]` Attribute

`AreWeDoomd.Api` katmanında yaşar. Sadece veri taşır, logic içermez.

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class PublishActivityAttribute(
    ActivityType activityType,
    ActorType actorType,
    ActivityObjectType objectType,
    string? objectIdParam,
    ActivityTargetType targetType,
    string? targetIdParam) : Attribute
```

| Parametre | Açıklama |
|---|---|
| `activityType` | Ne tür bir aktivite (örn. `CommentCreated`) |
| `actorType` | Aktörün tipi (örn. `Human`) |
| `objectType` | Nesnenin tipi (örn. `Comment`) |
| `objectIdParam` | Route key adı; POST ile yaratılan nesnelerde `null` olabilir |
| `targetType` | Hedefin tipi (örn. `Post`) |
| `targetIdParam` | Route key adı (örn. `"postId"`) |

Örnek kullanım:

```csharp
[HttpPost("{postId:guid}/comments")]
[Authorize]
[PublishActivity(
    ActivityType.CommentCreated,
    ActorType.Human,
    ActivityObjectType.Comment, objectIdParam: null,
    ActivityTargetType.Post,   targetIdParam: "postId")]
public async Task<ActionResult<CommentResponse>> CreateComment(...)
```

---

## 5. `ActivityEmissionFilter`

`AreWeDoomd.Api` katmanında yaşar. DI üzerinden `INotificationEngine` ve `IAgentNotifier` alır.

### 5.1 Akış

```
[ActionExecuted]
  ├─ Exception var mı?             → çık
  ├─ HTTP status 2xx mi?           → değilse çık
  ├─ [PublishActivity] var mı?     → yoksa çık
  ├─ Route values'dan ObjectId + TargetId oku (null ise "")
  ├─ JWT claim'lerinden ActorId + ActorDisplayName oku
  ├─ ActivityContext kur
  └─ _ = Task.Run(() =>
           engine.ComputeAsync(context)
             .ContinueWith(t => notifier.NotifyAsync(t.Result)))
```

### 5.2 Actor çözümleme

| Alan | Kaynak | Fallback |
|---|---|---|
| `ActorId` | `ClaimTypes.NameIdentifier` | `""` |
| `ActorDisplayName` | `ClaimTypes.Name` | `ActorId` |

### 5.3 Kayıt

Filter `IAsyncActionFilter` implement eder, DI'a `AddScoped<ActivityEmissionFilter>()` ile kaydedilir ve **globally** `options.Filters.AddService<ActivityEmissionFilter>()` üzerinden eklenir. Filter, her action'da önce `[PublishActivity]` attribute'u arar; yoksa erken çıkar. Bu sayede endpoint'lere ek attribute eklemek gerekmez — sadece `[PublishActivity]` yeterlidir.

---

## 6. `PassthroughNotificationEngine`

`AreWeDoomd.Infrastructure` katmanında yaşar. `INotificationEngine` implement eder.

```csharp
// ActivityContext'ten minimal bir ActivityNotification üretir
// Recipients listesi boş — engine implement edilince kaldırılır
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

---

## 7. Tam Akış

```
HTTP Request (örn. POST /api/posts/{postId}/comments)
    ↓
CommentsController.CreateComment
    ↓ (200 OK)
ActivityEmissionFilter.OnActionExecuted
    ├─ ActivityContext kur
    └─ fire-and-forget →
           PassthroughNotificationEngine.ComputeAsync(context)
               → ActivityNotification (Recipients: [])
           IAgentNotifier.NotifyAsync(notification)
               → SignalR → AgentService
AgentService
    └─ Console.WriteLine / Logger.LogInformation (mevcut)
```

---

## 8. Yeni Dosyalar

| Dosya | Katman |
|---|---|
| `Api/Filters/PublishActivityAttribute.cs` | Api |
| `Api/Filters/ActivityEmissionFilter.cs` | Api |
| `Infrastructure/Notifications/PassthroughNotificationEngine.cs` | Infrastructure |

---

## 9. Güncellenen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `ActivityNotifications.Contracts/` (tüm dosyalar) | namespace güncellenir |
| `ActivityNotifications.Contracts/ActivityNotification.cs` | `EventNotification` → `ActivityNotification` |
| `ActivityNotifications.Contracts/ActivityTarget.cs` | `OwnerId` kaldırılır |
| `Application/Notifications/Engine/ActivityContext.cs` | `TargetOwnerId` kaldırılır |
| `Application/Notifications/Engine/INotificationEngine.cs` | dönüş tipi güncellenir |
| `Application/Notifications/Dispatching/INotificationDispatcher.cs` | parametre tipi güncellenir |
| `Application/Common/Interfaces/IAgentNotifier.cs` | parametre tipi güncellenir |
| `Api/Realtime/SignalRAgentNotifier.cs` | parametre tipi güncellenir |
| `Api/Realtime/IAgentNotificationClient.cs` | parametre tipi güncellenir |
| `Api/Program.cs` | DI kaydı + dev endpoint güncellenir |
| `AgentService/AgentNotificationListener.cs` | tip güncellenir |
| `Controllers/CommentsController.cs` | `[PublishActivity]` eklenir |
| Tüm `.csproj` ve solution dosyaları | proje referansı güncellenir |
| Test dosyaları | tip referansları güncellenir |

---

## 10. Kapsam Dışı

- `INotificationEngine` gerçek implementasyonu (kurallar, DB sorguları)
- `INotificationDispatcher` implementasyonu
- Human bildirim kanalı
- `objectIdParam: null` durumunun çözümü (POST ile yaratılan nesneler)
- Filter'ın global kayıt stratejisi (ileride değerlendirilebilir)

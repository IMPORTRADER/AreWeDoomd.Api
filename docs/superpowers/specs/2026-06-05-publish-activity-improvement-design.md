# PublishActivity İyileştirmesi Tasarımı

> Tarih: 2026-06-05
> Durum: Onaylandı
> Kapsam: `[PublishActivity]` attribute + filter ergonomisi; actor tipini claim'den türetme, object bilgisini response body'den yakalama
> Önceki tasarım: `2026-06-04-publish-activity-filter-design.md`

---

## 1. Amaç

`[PublishActivity]` attribute'unun mevcut hali, çalışma zamanına ait bilgileri statik olarak taşıyor ve POST ile yaratılan nesnelerin id'sini yakalayamıyor. Bu tasarım üç somut zayıflığı giderir:

1. **Actor tipi sabit yazılıyor** (`ActorType.Human`) — oysa aynı endpoint'i hem insan hem Ai agent çağırabilir. Doğru kaynak, isteği yapan token'dır.
2. **Yeni yaratılan nesnenin id'si yakalanamıyor** — `objectIdParam: null` veriliyor ve `ObjectId` boş (`""`) kalıyor. Yeni Comment'in GUID'i sadece response body'sinde var.
3. **`ObjectTextPreview` hep `null`** — engine'e gönderilen önizleme metni hiç doldurulmuyor.

Örnek endpoint: `CommentsController.CreateComment`.

---

## 2. Kararlar

| Konu | Karar | Gerekçe |
|---|---|---|
| Actor tipi | Attribute'tan kaldırılır; JWT `Role` claim'inden türetilir | `JwtAccessTokenGenerator` zaten her token'a `ClaimTypes.Role = user.UserType.ToString()` koyuyor. Aynı endpoint'i Human da Ai da çağırabilir. |
| Geçersiz/eksik actor | Error loglanır, **aktivite atlanır** (notification üretilmez) | Yanlış actor tipiyle bildirim göndermemek; veri temizliği öncelikli. HTTP yanıtı etkilenmez (fire-and-forget). |
| Object id + tip + preview | Response body'den marker interface (`IActivityObjectCarrier`) ile okunur | Yaratılan nesnenin id'si istek anında route'ta yok; sadece response'ta var. Nesne kendi kimliğini ve tipini bilir. |
| `ObjectType` | Attribute'tan kaldırılır; carrier sağlar | Response DTO'su kendi tipini bilir (CommentResponse = Comment). |
| `ActivityType` / `TargetType` | Attribute'ta explicit kalır | Mevcut hal yeterince açık; fazla bilgi sorun değil. Eşlemeyi merkezileştirmek bu turun kapsamı dışı. |
| `targetIdParam` | String olarak kalır | Refactor güvenliği bu turda kapsam dışı. İleride startup guard değerlendirilebilir. |
| Filter adı | `ActivityEmissionFilter` → **`ActivityPublishingFilter`** | Attribute (`PublishActivity`) ile aynı kelimeyi paylaşır, terminoloji tutarlılığı. |

---

## 3. `PublishActivityAttribute` (sadeleşme)

`actorType`, `objectType`, `objectIdParam` parametreleri kaldırılır. 6 parametreden 3'e iner.

```csharp
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

Kullanım:

```csharp
[PublishActivity(ActivityType.CommentCreated, ActivityTargetType.Post, targetIdParam: "postId")]
```

---

## 4. `IActivityObjectCarrier` (yeni marker interface)

`AreWeDoomd.Api` katmanında yaşar (`Api/Filters/IActivityObjectCarrier.cs`). Response DTO'ları, aktivitenin object kimliğini buradan taşır.

```csharp
public interface IActivityObjectCarrier
{
    string ActivityObjectId { get; }
    ActivityObjectType ActivityObjectType { get; }
    string? ActivityObjectTextPreview => null;   // default: önizleme yok
}
```

`CommentResponse` bunu **explicit** implement eder — public API yüzeyini kirletmez:

```csharp
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

Bonus: Eskiden hep `null` geçilen `ObjectTextPreview` artık yorumun içeriğiyle dolar.

---

## 5. Actor tipi türetme

Token üretiminde (`JwtAccessTokenGenerator.cs:34`) zaten mevcut:

```csharp
new Claim(ClaimTypes.Role, user.UserType.ToString())  // "Unknown" | "Ai" | "Human"
```

Filter bu claim'i okuyup `ActorType`'a çevirir:

| Token'daki `role` | Domain `UserType` | → `ActorType` | Davranış |
|---|---|---|---|
| `"Ai"` | `Ai` | `Agent` | aktivite üretilir |
| `"Human"` | `Human` | `Human` | aktivite üretilir |
| `"Unknown"` / yok / boş | `Unknown` | — | **error loglanır, aktivite atlanır** |

---

## 6. `ActivityPublishingFilter` (güncellenmiş)

`AreWeDoomd.Api` katmanında yaşar. DI üzerinden `INotificationEngine`, `IAgentNotifier` ve (yeni) `ILogger<ActivityPublishingFilter>` alır.

```csharp
public sealed class ActivityPublishingFilter(
    INotificationEngine engine,
    IAgentNotifier notifier,
    ILogger<ActivityPublishingFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        var attribute = executed.ActionDescriptor.EndpointMetadata
            .OfType<PublishActivityAttribute>().FirstOrDefault();

        if (attribute is null) return;
        if (executed.Exception is not null) return;
        if (executed.Result is IStatusCodeActionResult { StatusCode: int code } && (code < 200 || code >= 300)) return;

        var user = executed.HttpContext.User;

        // 1) Actor tipi — Role claim'inden; güvenilmezse logla ve aktiviteyi atla
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (role != nameof(UserType.Ai) && role != nameof(UserType.Human))
        {
            logger.LogError("PublishActivity: geçersiz/eksik role claim '{Role}', aktivite atlandı.", role);
            return;
        }
        var actorType = role == nameof(UserType.Ai) ? ActorType.Agent : ActorType.Human;

        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var actorDisplayName = user.FindFirstValue(ClaimTypes.Name) ?? actorId;

        // 2) Object — response body'deki carrier'dan (id + tip + preview)
        var objectId = "";
        ActivityObjectType objectType = default;
        string? objectTextPreview = null;
        if (executed.Result is ObjectResult { Value: IActivityObjectCarrier o })
        {
            objectId = o.ActivityObjectId;
            objectType = o.ActivityObjectType;
            objectTextPreview = o.ActivityObjectTextPreview;
        }

        // 3) Target — route value'dan (mevcut davranış)
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

### 6.1 Result tipi notu

`ControllerResultExtensions.ToActionResult` başarı durumunda `controller.Ok(...)` → `OkObjectResult` (`: ObjectResult`) döndürür; `.Value` mapping sonucu DTO'dur. Bu yüzden `executed.Result is ObjectResult { Value: IActivityObjectCarrier o }` deseni çalışır.

### 6.2 Açık nokta — carrier'sız endpoint

Carrier implement etmeyen bir response'ta `objectType = default` (`ActivityObjectType.Comment`, enum 0) kalır ve `objectId = ""` olur. Bizim kapsamımızda `CreateComment` her zaman carrier döndürdüğü için sorun değil. Carrier'sız endpoint'ler bu mekanizmaya dahil olduğunda ayrıca ele alınır (bu tur kapsam dışı).

---

## 7. Etkilenen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Api/Filters/PublishActivityAttribute.cs` | `actorType`, `objectType`, `objectIdParam` parametreleri kaldırılır |
| `Api/Filters/IActivityObjectCarrier.cs` | **yeni** marker interface |
| `Api/Filters/ActivityEmissionFilter.cs` → `ActivityPublishingFilter.cs` | dosya + sınıf adı değişir; `ILogger` eklenir; actor claim'den, object body'den |
| `Api/Contracts/Comments/CommentResponse.cs` | `IActivityObjectCarrier` explicit implement eder |
| `Controllers/CommentsController.cs` | `[PublishActivity]` 3 parametreye iner |
| `Program.cs` | DI kaydı `ActivityPublishingFilter`'a güncellenir (`AddScoped` + `AddService`) |
| `tests/.../ActivityEmissionFilterTests.cs` → `ActivityPublishingFilterTests.cs` | yeni imza + yeni senaryolar |

---

## 8. Testler

`ActivityPublishingFilterTests` (yeniden adlandırılmış). Mevcut testler yeni imzaya uyarlanır, şu senaryolar eklenir/güncellenir:

- `role: "Ai"` → `ActorType.Agent` ile `ActivityContext` üretilir.
- `role: "Human"` → `ActorType.Human`.
- `role: "Unknown"` / claim yok → error loglanır, **engine ve notifier hiç çağrılmaz** (aktivite atlanır).
- Response `IActivityObjectCarrier` ise → `ObjectId`, `ObjectType`, `ObjectTextPreview` body'den dolar.
- Non-2xx sonuç / exception / attribute yok → erken çıkış (mevcut davranış korunur).

---

## 9. Kapsam Dışı

- `ActivityType` ↔ `ObjectType`/`TargetType` eşlemesinin merkezileştirilmesi (explicit kalır).
- `targetIdParam` magic string'inin tip-güvenli hale getirilmesi (string kalır; startup guard ileride değerlendirilebilir).
- Carrier implement etmeyen endpoint'lerde object tipi/id davranışı.
- `INotificationEngine` / `INotificationDispatcher` gerçek implementasyonları.
- `UserType` enum'unun `Unknown` değerinin gerekliliği tartışması (kod içi mevcut soru notu).

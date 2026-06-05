# EventNotification DTO'ları ve Tespit Motoru Tasarımı

> **Not:** Bu doküman kısmen geçerliliğini yitirmiştir. Güncel kararlar için bkz:
> - `docs/superpowers/specs/2026-06-04-publish-activity-filter-design.md`
>
> Değişenler: `EventNotification` → `ActivityNotification`, `ActivityTypes` static class → `ActivityType` enum,
> `string Type` alanları → enum tipleri, `ActivityTarget.OwnerId` kaldırıldı, `ActivityContext.TargetOwnerId` kaldırıldı.

> Tarih: 2026-06-04  
> Durum: Onaylandı  
> Kapsam: `EventNotifications.Contracts` DTO yeniden yapılandırması + Application katmanında engine/dispatcher arayüzleri

---

## 1. Amaç

Task 1'de kurulan SignalR taşıma katmanının sözleşmesini (`AgentEventNotification`) kaldırıp zengin bir activity stream formatına (`EventNotification`) geçmek; ileride implement edilecek tespit motorunun (engine) yerini ve arayüzünü belirlemek.

---

## 2. Kararlar

| Konu | Karar | Gerekçe |
| --- | --- | --- |
| Eski sözleşme | `AgentEventNotification` + `AgentEventType` kaldırılır | Yeni zengin format onların yerini alır |
| Yeni wire formatı | `EventNotification` (zengin, aşağıda tanımlı) | AgentService ek sorgu yapmadan işleme başlayabilsin |
| `activity_type` modeli | `ActivityTypes` static sabit sınıfı (string) | Açık uçlu key sistemi; enum serileştirme karmaşıklığı yok |
| `priority` modeli | `NotificationPriority` enum | Sınırlı küme; tip güvenliği değerli |
| Engine konumu | `AreWeDoomd.Application` katmanı | Repository arayüzlerine erişim gerektirir; ayrı proje YAGNI |
| Engine uygulaması | Bu task'ta implement edilmez | Arayüz ve klasör yapısı şimdiden yerleşir |
| Hub method adı | `ReceiveAgentEvent` → `ReceiveEvent` | Artık yalnızca agent değil, genel event notification |
| Wire protokolü | **MessagePack** (binary) | İki taraf da bizim; JSON naming convention karmaşıklığı yok; daha hızlı/küçük |

---

## 3. `EventNotifications.Contracts` — Yeni DTO Yapısı

### 3.1 `EventNotification` (ana record)

```csharp
public sealed record EventNotification(
    string ActivityId,
    string ActivityType,
    DateTimeOffset OccurredAt,
    ActivityActor Actor,
    ActivityObject Object,
    ActivityTarget Target,
    IReadOnlyList<NotificationRecipient> Recipients);
```

### 3.2 Yardımcı record'lar

```csharp
public sealed record ActivityActor(
    string Id,
    string Type,
    string DisplayName);

public sealed record ActivityObject(
    string Id,
    string Type,
    string? TextPreview);

public sealed record ActivityTarget(
    string Id,
    string Type,
    string OwnerId);

public sealed record NotificationRecipient(
    string UserId,
    string Reason,
    string Template,
    IReadOnlyDictionary<string, string> Params,
    string DedupeKey,
    NotificationPriority Priority);
```

### 3.3 `NotificationPriority` enum

```csharp
public enum NotificationPriority
{
    Normal = 0,
    High = 1,
    Critical = 2
}
```

MessagePack ile enum integer olarak iletilir — her iki taraf da aynı enum tanımını paylaştığı için ek yapılandırma gerekmez.

### 3.x Serileştirme: MessagePack

İki taraf da bizim kodumuzu ve aynı `EventNotifications.Contracts` tiplerini kullandığı için wire format bir implementation detail'dir. JSON naming convention karmaşıklığını tamamen ortadan kaldırmak için **MessagePack** (binary) tercih edilir.

**Paket:** `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` (hem API hem AgentService).

**API (`Program.cs`):**
```csharp
builder.Services.AddSignalR()
    .AddMessagePackProtocol();
```

**AgentService (`AgentNotificationListener.cs`):**
```csharp
new HubConnectionBuilder()
    .WithUrl(...)
    .AddMessagePackProtocol()
    .Build();
```

`ContractlessStandardResolver` (MessagePack'in varsayılanı) attribute gerektirmez; record'lar ve `IReadOnlyList`/`IReadOnlyDictionary` kutudan çıkar çalışır.

### 3.4 `ActivityTypes` static sabit sınıfı

```csharp
public static class ActivityTypes
{
    public const string CommentCreated = "comment.created";
    public const string PostLiked      = "post.liked";
    // ileride genişler
}
```

### 3.5 `AgentNotificationHubConstants` güncelleme

`ReceiveAgentEventMethod = "ReceiveAgentEvent"` → `"ReceiveEvent"` olarak güncellenir.

### 3.6 Kaldırılan tipler

- `AgentEventNotification.cs`
- `AgentEventType.cs`

### 3.7 Kavramsal payload (C# property adlarıyla)

Wire format binary (MessagePack) olduğu için JSON gösterimi yalnızca kavramsal referanstır:

```
ActivityId:   "act_123"
ActivityType: "comment.created"
OccurredAt:   2026-06-04T12:34:56Z
Actor:        { Id: "user_1", Type: "user", DisplayName: "Ali" }
Object:       { Id: "comment_99", Type: "comment", TextPreview: "Harika olmuş!" }
Target:       { Id: "post_42", Type: "post", OwnerId: "user_2" }
Recipients:
  - UserId:     "user_2"
    Reason:     "post_owner"
    Template:   "post.comment.created"
    Params:     { actor_name: "Ali", comment_preview: "Harika olmuş!", ... }
    DedupeKey:  "comment.created:comment_99:user_2"
    Priority:   Normal (0)
```

---

## 4. Application Katmanı — Engine ve Dispatcher Arayüzleri

### 4.1 Klasör yapısı

```
src/AreWeDoomd.Application/
 └─ Notifications/
     ├─ Engine/
     │   ├─ INotificationEngine.cs
     │   ├─ ActivityContext.cs
     │   └─ Rules/               ← ileride her activity tipi için birer rule
     └─ Dispatching/
         └─ INotificationDispatcher.cs
```

### 4.2 `ActivityContext`

Engine'e verilen ham girdi — MediatR handler'ı tarafından doldurulur:

```csharp
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

### 4.3 `INotificationEngine`

```csharp
public interface INotificationEngine
{
    Task<EventNotification> ComputeAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default);
}
```

### 4.4 `INotificationDispatcher`

Engine çıktısını alıp `UserType`'a göre yönlendirir:

```csharp
public interface INotificationDispatcher
{
    Task DispatchAsync(
        EventNotification notification,
        CancellationToken cancellationToken = default);
}
```

---

## 5. Tam Akış

```
MediatR Handler
    │  ActivityContext oluştur
    ▼
INotificationEngine.ComputeAsync()
    │  recipients hesaplanır, EventNotification döner
    ▼
INotificationDispatcher.DispatchAsync()
    │  recipient.UserType == Ai     → IAgentNotifier.NotifyAsync()  → SignalR → AgentService
    │  recipient.UserType == Human  → TODO (kapsam dışı)
    │  recipient.UserType == Unknown → yoksay
    ▼
AgentService: ReceiveEvent ile alır, loglar (ileride LLM)
```

---

## 6. Etkilenen Mevcut Dosyalar

| Dosya | Değişiklik |
| --- | --- |
| `EventNotifications.Contracts/AgentEventNotification.cs` | Kaldırılır |
| `EventNotifications.Contracts/AgentEventType.cs` | Kaldırılır |
| `EventNotifications.Contracts/AgentNotificationHubConstants.cs` | `ReceiveAgentEventMethod` güncellenir |
| `Application/Common/Interfaces/IAgentNotifier.cs` | `AgentEventNotification` → `EventNotification` |
| `Api/Realtime/IAgentNotificationClient.cs` | Metod imzası güncellenir |
| `Api/Realtime/SignalRAgentNotifier.cs` | Parametre tipi güncellenir |
| `Api/Program.cs` | `AddSignalR().AddMessagePackProtocol()` + dev tetikleyici güncellenir |
| `Api/AreWeDoomd.Api.csproj` | `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` paketi eklenir |
| `AgentService/AgentNotificationListener.cs` | `HubConnectionBuilder` `.AddMessagePackProtocol()` alır |
| `AgentService/AreWeDoomd.AgentService.csproj` | `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` paketi eklenir |
| `UnitTests/Realtime/AgentNotificationContractTests.cs` | Yeni tiplere göre güncellenir |
| `UnitTests/Realtime/AgentNotificationHubTests.cs` | `HubConnectionBuilder` `.AddMessagePackProtocol()` alır; yeni tiplere göre güncellenir |

---

## 7. Kapsam Dışı

- Engine implementasyonu (kurallar, DB sorguları)
- Dispatcher implementasyonu
- Belirli activity türlerinin wiring'i (comment.created vb.)
- Human bildirim kanalı

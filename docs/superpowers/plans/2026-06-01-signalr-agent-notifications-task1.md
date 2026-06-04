# Task 1 — SignalR Taşıma Katmanı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Api ile AgentService arasında, shared-secret ile korunan bir SignalR kanalı kurmak; Api `IAgentNotifier.NotifyAsync` çağırınca AgentService bildirimi alıp loglasın.

**Architecture:** Api bir `Hub<IAgentNotificationClient>` host eder ve hub yolunu shared-secret middleware ile korur. Application katmanında `IAgentNotifier` soyutlaması, Api'de `IHubContext` tabanlı `SignalRAgentNotifier` implementasyonu yaşar. AgentService generic host'a çevrilip `BackgroundService` olarak hub'a bağlanır, yeniden bağlanma açıktır. Mesaj sözleşmesi iki tarafın da referans aldığı ayrı `AreWeDoomd.EventNotifications.Contracts` projesinde durur.

**Tech Stack:** .NET 10, ASP.NET Core SignalR (server), `Microsoft.AspNetCore.SignalR.Client`, `Microsoft.Extensions.Hosting`, xUnit + Shouldly + `Microsoft.AspNetCore.Mvc.Testing`.

**Spec:** `docs/superpowers/specs/2026-06-01-signalr-agent-notifications-design.md`

**Konvansiyonlar (AGENTS.md):** Dosya başına tek sınıf; tüm kontrol akışında küme parantezi; `I` öneki interface'lerde; `_camelCase` private alanlar. Bu plandaki kod bunlara uyar.

---

## File Structure

**Yeni proje — `src/AreWeDoomd.EventNotifications.Contracts/`**
- `AreWeDoomd.EventNotifications.Contracts.csproj` — bağımsız class library (net10.0).
- `AgentEventType.cs` — olay türü enum'ı.
- `AgentEventNotification.cs` — zengin bildirim payload record'u.
- `AgentNotificationHubConstants.cs` — hub yolu, client metod adı, secret header adı sabitleri.

**Application**
- `src/AreWeDoomd.Application/Common/Interfaces/IAgentNotifier.cs` — bildirim soyutlaması (YENİ).
- `src/AreWeDoomd.Application/AreWeDoomd.Application.csproj` — contracts projesine ProjectReference eklenir.

**Api**
- `src/AreWeDoomd.Api/Realtime/IAgentNotificationClient.cs` — strongly-typed client sözleşmesi (YENİ).
- `src/AreWeDoomd.Api/Realtime/AgentNotificationHub.cs` — hub (YENİ).
- `src/AreWeDoomd.Api/Realtime/SignalRAgentNotifier.cs` — `IAgentNotifier` implementasyonu (YENİ).
- `src/AreWeDoomd.Api/Realtime/AgentSecretValidator.cs` — saf, test edilebilir secret karşılaştırma (YENİ).
- `src/AreWeDoomd.Api/Realtime/Options/AgentNotificationsOptions.cs` — config options (YENİ).
- `src/AreWeDoomd.Api/Program.cs` — SignalR + DI + MapHub + secret middleware + dev tetikleyici + `public partial class Program` (MODIFY).
- `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj` — contracts projesine ProjectReference (MODIFY).
- `src/AreWeDoomd.Api/appsettings.Development.json` — `AgentNotifications:SharedSecret` (MODIFY).

**AgentService**
- `src/AreWeDoomd.AgentService/Program.cs` — generic host'a dönüşüm (MODIFY).
- `src/AreWeDoomd.AgentService/AgentServiceOptions.cs` — hub URL + secret options (YENİ).
- `src/AreWeDoomd.AgentService/AgentNotificationListener.cs` — `BackgroundService` (YENİ).
- `src/AreWeDoomd.AgentService/appsettings.json` — hub URL + secret + logging (YENİ).
- `src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj` — paketler + contracts referansı + appsettings kopyalama (MODIFY).

**Tests**
- `tests/AreWeDoomd.UnitTests/Realtime/AgentSecretValidatorTests.cs` — saf birim testi (YENİ).
- `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationHubTests.cs` — bağlanırlık entegrasyon testi (YENİ).
- `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj` — Api referansı + test paketleri (MODIFY).

**Solution**
- `AreWeDoomd.Api.slnx` — yeni contracts projesi eklenir (MODIFY).

---

## Task 1.1: EventNotifications.Contracts projesi ve sözleşme tipleri

**Files:**
- Create: `src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/AgentEventType.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/AgentEventNotification.cs`
- Create: `src/AreWeDoomd.EventNotifications.Contracts/AgentNotificationHubConstants.cs`
- Modify: `AreWeDoomd.Api.slnx`
- Test: `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationContractTests.cs` (geçici, bu task'ta eklenip sonraki task'larda kalır)

- [ ] **Step 1: Proje dosyasını oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Enum'ı oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/AgentEventType.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public enum AgentEventType
{
    Unknown = 0,
    CommentCreatedOnAgentPost = 1
}
```

- [ ] **Step 3: Bildirim record'unu oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/AgentEventNotification.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record AgentEventNotification(
    AgentEventType EventType,
    IReadOnlyList<Guid> TargetAgentIds,
    Guid PostId,
    Guid? CommentId,
    Guid ActorUserId,
    string ContentPreview,
    DateTimeOffset OccurredAt);
```

- [ ] **Step 4: Sabitleri oluştur**

`src/AreWeDoomd.EventNotifications.Contracts/AgentNotificationHubConstants.cs`:

```csharp
namespace AreWeDoomd.EventNotifications.Contracts;

public static class AgentNotificationHubConstants
{
    public const string HubPath = "/hubs/agent-notifications";
    public const string ReceiveAgentEventMethod = "ReceiveAgentEvent";
    public const string SecretHeaderName = "X-Agent-Secret";
}
```

- [ ] **Step 5: Projeyi solution'a ekle**

Run:
```bash
dotnet sln AreWeDoomd.Api.slnx add src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj
```
Expected: "Project ... added to the solution."

- [ ] **Step 6: Projenin derlendiğini doğrula**

Run:
```bash
dotnet build src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj
```
Expected: Build succeeded, 0 Error.

- [ ] **Step 7: Serileştirme round-trip testini yaz (önce başarısız olacak)**

> Not: `AgentEventNotification` bir `IReadOnlyList` alanı taşıdığı için record value-equality yapısal olmaz; bu yüzden alanları tek tek assert ediyoruz, record eşitliği kullanmıyoruz. Test projesi henüz contracts'ı görmüyor; bir sonraki adımda referans eklenecek.

`tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationContractTests.cs`:

```csharp
using System.Text.Json;
using AreWeDoomd.EventNotifications.Contracts;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class AgentNotificationContractTests
{
    [Fact]
    public void AgentEventNotification_SurvivesJsonRoundTrip()
    {
        var original = new AgentEventNotification(
            AgentEventType.CommentCreatedOnAgentPost,
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            PostId: Guid.NewGuid(),
            CommentId: Guid.NewGuid(),
            ActorUserId: Guid.NewGuid(),
            ContentPreview: "merhaba dunya",
            OccurredAt: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<AgentEventNotification>(json);

        roundTripped.ShouldNotBeNull();
        roundTripped!.EventType.ShouldBe(original.EventType);
        roundTripped.TargetAgentIds.ShouldBe(original.TargetAgentIds);
        roundTripped.PostId.ShouldBe(original.PostId);
        roundTripped.CommentId.ShouldBe(original.CommentId);
        roundTripped.ActorUserId.ShouldBe(original.ActorUserId);
        roundTripped.ContentPreview.ShouldBe(original.ContentPreview);
        roundTripped.OccurredAt.ShouldBe(original.OccurredAt);
    }
}
```

- [ ] **Step 8: Test projesine contracts referansını ekle**

Run:
```bash
dotnet add tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj reference src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj
```
Expected: "Reference ... added to the project."

- [ ] **Step 9: Testi çalıştır, geçtiğini doğrula**

Run:
```bash
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~AgentNotificationContractTests"
```
Expected: PASS (1 test).

- [ ] **Step 10: Commit**

```bash
git add src/AreWeDoomd.EventNotifications.Contracts AreWeDoomd.Api.slnx tests/AreWeDoomd.UnitTests
git commit -m "feat: add EventNotifications.Contracts project with agent notification types"
```

---

## Task 1.2: Application — IAgentNotifier soyutlaması

**Files:**
- Create: `src/AreWeDoomd.Application/Common/Interfaces/IAgentNotifier.cs`
- Modify: `src/AreWeDoomd.Application/AreWeDoomd.Application.csproj`

- [ ] **Step 1: Application'a contracts referansını ekle**

Run:
```bash
dotnet add src/AreWeDoomd.Application/AreWeDoomd.Application.csproj reference src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj
```
Expected: "Reference ... added to the project."

- [ ] **Step 2: Interface'i oluştur**

`src/AreWeDoomd.Application/Common/Interfaces/IAgentNotifier.cs`:

```csharp
using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAgentNotifier
{
    Task NotifyAsync(AgentEventNotification notification, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Application'ın derlendiğini doğrula**

Run:
```bash
dotnet build src/AreWeDoomd.Application/AreWeDoomd.Application.csproj
```
Expected: Build succeeded, 0 Error.

- [ ] **Step 4: Commit**

```bash
git add src/AreWeDoomd.Application
git commit -m "feat: add IAgentNotifier abstraction in Application layer"
```

---

## Task 1.3: Api — Hub, notifier, secret koruması ve dev tetikleyici

**Files:**
- Create: `src/AreWeDoomd.Api/Realtime/Options/AgentNotificationsOptions.cs`
- Create: `src/AreWeDoomd.Api/Realtime/AgentSecretValidator.cs`
- Create: `src/AreWeDoomd.Api/Realtime/IAgentNotificationClient.cs`
- Create: `src/AreWeDoomd.Api/Realtime/AgentNotificationHub.cs`
- Create: `src/AreWeDoomd.Api/Realtime/SignalRAgentNotifier.cs`
- Modify: `src/AreWeDoomd.Api/Program.cs`
- Modify: `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`
- Modify: `src/AreWeDoomd.Api/appsettings.Development.json`
- Modify: `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`
- Test: `tests/AreWeDoomd.UnitTests/Realtime/AgentSecretValidatorTests.cs`

- [ ] **Step 1: Api'ye contracts referansını ekle**

Run:
```bash
dotnet add src/AreWeDoomd.Api/AreWeDoomd.Api.csproj reference src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj
```
Expected: "Reference ... added to the project."

- [ ] **Step 2: Options sınıfını oluştur**

`src/AreWeDoomd.Api/Realtime/Options/AgentNotificationsOptions.cs`:

```csharp
namespace AreWeDoomd.Api.Realtime.Options;

public sealed class AgentNotificationsOptions
{
    public const string SectionName = "AgentNotifications";

    public string SharedSecret { get; set; } = string.Empty;
}
```

- [ ] **Step 3: AgentSecretValidator birim testini yaz (önce başarısız olacak)**

Önce test projesine Api referansını ekle:
```bash
dotnet add tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj reference src/AreWeDoomd.Api/AreWeDoomd.Api.csproj
```
Expected: "Reference ... added to the project."

`tests/AreWeDoomd.UnitTests/Realtime/AgentSecretValidatorTests.cs`:

```csharp
using AreWeDoomd.Api.Realtime;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class AgentSecretValidatorTests
{
    [Fact]
    public void IsValid_ReturnsTrue_WhenSecretsMatch()
    {
        AgentSecretValidator.IsValid("super-secret", "super-secret").ShouldBeTrue();
    }

    [Theory]
    [InlineData("super-secret", "wrong-secret")]
    [InlineData("super-secret", "")]
    [InlineData("", "super-secret")]
    [InlineData(null, "super-secret")]
    [InlineData("super-secret", null)]
    public void IsValid_ReturnsFalse_WhenSecretsDoNotMatchOrMissing(string? provided, string? expected)
    {
        AgentSecretValidator.IsValid(provided, expected).ShouldBeFalse();
    }
}
```

- [ ] **Step 4: Testi çalıştır, derlenmediği için başarısız olduğunu doğrula**

Run:
```bash
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~AgentSecretValidatorTests"
```
Expected: FAIL — derleme hatası (`AgentSecretValidator` tipi yok).

- [ ] **Step 5: AgentSecretValidator'ı oluştur**

`src/AreWeDoomd.Api/Realtime/AgentSecretValidator.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace AreWeDoomd.Api.Realtime;

public static class AgentSecretValidator
{
    public static bool IsValid(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
```

- [ ] **Step 6: Testi çalıştır, geçtiğini doğrula**

Run:
```bash
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~AgentSecretValidatorTests"
```
Expected: PASS (6 test).

- [ ] **Step 7: Client sözleşmesini oluştur**

`src/AreWeDoomd.Api/Realtime/IAgentNotificationClient.cs`:

```csharp
using AreWeDoomd.EventNotifications.Contracts;

namespace AreWeDoomd.Api.Realtime;

public interface IAgentNotificationClient
{
    Task ReceiveAgentEvent(AgentEventNotification notification);
}
```

- [ ] **Step 8: Hub'ı oluştur**

> Secret doğrulaması Program.cs'teki middleware'de yapıldığı için hub'ın ek bir gövdeye ihtiyacı yoktur; strongly-typed taban yeterlidir.

`src/AreWeDoomd.Api/Realtime/AgentNotificationHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace AreWeDoomd.Api.Realtime;

public sealed class AgentNotificationHub : Hub<IAgentNotificationClient>
{
}
```

- [ ] **Step 9: Notifier implementasyonunu oluştur**

`src/AreWeDoomd.Api/Realtime/SignalRAgentNotifier.cs`:

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

    public Task NotifyAsync(AgentEventNotification notification, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.ReceiveAgentEvent(notification);
    }
}
```

- [ ] **Step 10: appsettings.Development.json'a shared secret ekle**

`src/AreWeDoomd.Api/appsettings.Development.json` içine, `Jwt` bloğunun yanına ekle:

```json
    "AgentNotifications": {
        "SharedSecret": "dev-agent-shared-secret-change-me"
    },
```

(Mevcut JSON'a uygun şekilde virgülleri ayarla; geçerli JSON kalmalı.)

- [ ] **Step 11: Program.cs'i güncelle**

`src/AreWeDoomd.Api/Program.cs` — using'leri ekle (dosyanın başına):

```csharp
using AreWeDoomd.Api.Realtime;
using AreWeDoomd.Api.Realtime.Options;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.EventNotifications.Contracts;
```

Servis kayıtları — `builder.Services.AddInfrastructure(builder.Configuration);` satırından **sonra** ekle:

```csharp
    builder.Services.Configure<AgentNotificationsOptions>(
        builder.Configuration.GetSection(AgentNotificationsOptions.SectionName));
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IAgentNotifier, SignalRAgentNotifier>();
```

Secret koruma middleware'i + hub eşlemesi — `app.MapControllers();` satırından **önce** ekle:

```csharp
    var agentSecret = builder.Configuration
        .GetSection(AgentNotificationsOptions.SectionName)
        .Get<AgentNotificationsOptions>()?.SharedSecret;

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments(AgentNotificationHubConstants.HubPath))
        {
            var provided = context.Request
                .Headers[AgentNotificationHubConstants.SecretHeaderName]
                .ToString();

            if (!AgentSecretValidator.IsValid(provided, agentSecret))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await next();
    });

    app.MapHub<AgentNotificationHub>(AgentNotificationHubConstants.HubPath);
```

Dev-only tetikleyici (manuel uçtan uca doğrulama için) — `app.MapHub(...)` satırından sonra, `if (app.Environment.IsDevelopment())` bloğuna benzer şekilde ekle:

```csharp
    if (app.Environment.IsDevelopment())
    {
        app.MapPost("/dev/agent-notifications/test", async (IAgentNotifier notifier) =>
        {
            var notification = new AgentEventNotification(
                AgentEventType.CommentCreatedOnAgentPost,
                new[] { Guid.NewGuid() },
                PostId: Guid.NewGuid(),
                CommentId: Guid.NewGuid(),
                ActorUserId: Guid.NewGuid(),
                ContentPreview: "dev test notification",
                OccurredAt: DateTimeOffset.UtcNow);

            await notifier.NotifyAsync(notification);
            return Results.Accepted();
        });
    }
```

Son olarak dosyanın **en altına** (top-level statement'lardan sonra) test erişimi için ekle:

```csharp
public partial class Program
{
}
```

- [ ] **Step 12: Api'nin derlendiğini doğrula**

Run:
```bash
dotnet build src/AreWeDoomd.Api/AreWeDoomd.Api.csproj
```
Expected: Build succeeded, 0 Error.

- [ ] **Step 13: Commit**

```bash
git add src/AreWeDoomd.Api tests/AreWeDoomd.UnitTests
git commit -m "feat: add SignalR agent notification hub, notifier and shared-secret guard"
```

---

## Task 1.4: AgentService — generic host ve hub dinleyicisi

**Files:**
- Modify: `src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj`
- Create: `src/AreWeDoomd.AgentService/AgentServiceOptions.cs`
- Create: `src/AreWeDoomd.AgentService/AgentNotificationListener.cs`
- Create: `src/AreWeDoomd.AgentService/appsettings.json`
- Modify: `src/AreWeDoomd.AgentService/Program.cs`

- [ ] **Step 1: Paketleri ve referansı ekle**

Run:
```bash
dotnet add src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj package Microsoft.Extensions.Hosting
dotnet add src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj package Microsoft.AspNetCore.SignalR.Client
dotnet add src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj reference src/AreWeDoomd.EventNotifications.Contracts/AreWeDoomd.EventNotifications.Contracts.csproj
```
Expected: Her komut için başarı mesajı.

- [ ] **Step 2: appsettings.json'u kopyalanacak şekilde işaretle**

`src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj` içine yeni bir ItemGroup ekle:

```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 3: Options sınıfını oluştur**

`src/AreWeDoomd.AgentService/AgentServiceOptions.cs`:

```csharp
namespace AreWeDoomd.AgentService;

public sealed class AgentServiceOptions
{
    public const string SectionName = "AgentNotifications";

    public string HubUrl { get; set; } = string.Empty;

    public string SharedSecret { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Hub dinleyicisini oluştur**

`src/AreWeDoomd.AgentService/AgentNotificationListener.cs`:

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
            .WithAutomaticReconnect()
            .Build();

        _connection.On<AgentEventNotification>(
            AgentNotificationHubConstants.ReceiveAgentEventMethod,
            notification =>
            {
                _logger.LogInformation(
                    "Received agent event {EventType} for post {PostId} with {TargetCount} target(s)",
                    notification.EventType,
                    notification.PostId,
                    notification.TargetAgentIds.Count);
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

- [ ] **Step 5: appsettings.json'u oluştur**

`src/AreWeDoomd.AgentService/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.SignalR.Client": "Warning"
    }
  },
  "AgentNotifications": {
    "HubUrl": "http://localhost:5188/hubs/agent-notifications",
    "SharedSecret": "dev-agent-shared-secret-change-me"
  }
}
```

> `HubUrl` Api'nin `launchSettings.json`'daki http profili (`http://localhost:5188`) ile eşleşir. `SharedSecret`, Api'nin `appsettings.Development.json`'daki değerle birebir aynı olmalıdır.

- [ ] **Step 6: Program.cs'i generic host'a çevir**

`src/AreWeDoomd.AgentService/Program.cs` (tamamını değiştir):

```csharp
using AreWeDoomd.AgentService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentServiceOptions>(
    builder.Configuration.GetSection(AgentServiceOptions.SectionName));
builder.Services.AddHostedService<AgentNotificationListener>();

var host = builder.Build();
host.Run();
```

- [ ] **Step 7: AgentService'in derlendiğini doğrula**

Run:
```bash
dotnet build src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj
```
Expected: Build succeeded, 0 Error.

- [ ] **Step 8: Commit**

```bash
git add src/AreWeDoomd.AgentService
git commit -m "feat: convert AgentService to generic host with SignalR hub listener"
```

---

## Task 1.5: Bağlanırlık entegrasyon testi (auth dahil)

**Files:**
- Modify: `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`
- Create: `tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationHubTests.cs`

- [ ] **Step 1: Test paketlerini ekle**

Run:
```bash
dotnet add tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj package Microsoft.AspNetCore.SignalR.Client
```
Expected: Her iki komut için başarı mesajı. (Api referansı Task 1.3 Step 3'te eklendi.)

- [ ] **Step 2: Entegrasyon testini yaz (önce başarısız olacak)**

> Test, WebSocket yerine **Long Polling** transport'u kullanır; bu, `TestServer`'ın `HttpMessageHandler`'ı üzerinden güvenilir çalışır ve secret header her HTTP isteğinde gönderilir. DB'ye dokunulmadığı için dummy bir connection string yeterlidir.

`tests/AreWeDoomd.UnitTests/Realtime/AgentNotificationHubTests.cs`:

```csharp
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.EventNotifications.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;
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
            .Build();
    }

    [Fact]
    public async Task ReceivesNotification_WhenSecretValid()
    {
        await using var connection = BuildConnection(Secret);

        var tcs = new TaskCompletionSource<AgentEventNotification>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<AgentEventNotification>(
            AgentNotificationHubConstants.ReceiveAgentEventMethod,
            notification => tcs.TrySetResult(notification));

        await connection.StartAsync();

        var notifier = _factory.Services.GetRequiredService<IAgentNotifier>();
        var sent = new AgentEventNotification(
            AgentEventType.CommentCreatedOnAgentPost,
            new[] { Guid.NewGuid() },
            PostId: Guid.NewGuid(),
            CommentId: Guid.NewGuid(),
            ActorUserId: Guid.NewGuid(),
            ContentPreview: "integration test",
            OccurredAt: DateTimeOffset.UtcNow);

        await notifier.NotifyAsync(sent);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(tcs.Task, "notification was not received within timeout");

        var received = await tcs.Task;
        received.EventType.ShouldBe(sent.EventType);
        received.PostId.ShouldBe(sent.PostId);
        received.TargetAgentIds.ShouldBe(sent.TargetAgentIds);
    }

    [Fact]
    public async Task ConnectionRejected_WhenSecretInvalid()
    {
        await using var connection = BuildConnection("wrong-secret");

        await Should.ThrowAsync<Exception>(async () => await connection.StartAsync());
    }
}
```

- [ ] **Step 3: Testleri çalıştır, geçtiğini doğrula**

Run:
```bash
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~AgentNotificationHubTests"
```
Expected: PASS (2 test). Pozitif testte bildirim alınır; negatif testte `StartAsync` 401 nedeniyle exception fırlatır.

- [ ] **Step 4: Tüm test paketinin yeşil olduğunu doğrula**

Run:
```bash
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj
```
Expected: PASS — yeni testler dahil tüm testler geçer.

- [ ] **Step 5: Commit**

```bash
git add tests/AreWeDoomd.UnitTests
git commit -m "test: add SignalR agent notification hub connectivity integration tests"
```

---

## Manuel Uçtan Uca Doğrulama (opsiyonel ama önerilir)

1. Api'yi başlat: `dotnet run --project src/AreWeDoomd.Api` (http profili, `http://localhost:5188`).
2. Ayrı bir terminalde AgentService'i başlat: `dotnet run --project src/AreWeDoomd.AgentService`.
   - Log'da `Connected to agent notification hub at http://localhost:5188/hubs/agent-notifications` görülmeli.
3. Dev tetikleyiciyi çağır: `curl -X POST http://localhost:5188/dev/agent-notifications/test`.
4. AgentService log'unda `Received agent event CommentCreatedOnAgentPost ...` satırı görülmeli.

---

## Self-Review Notları

- **Spec kapsamı:** Topoloji (Api host / AgentService client) ✓ (1.3, 1.4); ayrı contracts projesi ✓ (1.1); `IAgentNotifier` Application + `SignalRAgentNotifier` Api ✓ (1.2, 1.3); shared-secret doğrulama ✓ (1.3 middleware + validator); zengin payload sözleşmesi ✓ (1.1); generic host + auto-reconnect dinleyici ✓ (1.4); bağlanırlık + auth testi ✓ (1.5); fire-and-forget `Clients.All` ✓ (1.3).
- **Kapsam dışı (spec bölüm 11):** dayanıklılık/kuyruk, backplane, LLM mantığı, gerçek tespit motoru, insan bildirimleri — bu plana dahil değil.
- **Tip tutarlılığı:** `AgentEventNotification` alanları (EventType, TargetAgentIds, PostId, CommentId, ActorUserId, ContentPreview, OccurredAt) tüm task'larda aynı; sabit adları (`HubPath`, `ReceiveAgentEventMethod`, `SecretHeaderName`) tek kaynaktan (`AgentNotificationHubConstants`) kullanılıyor.
- **Not — paket sürümleri:** `dotnet add package` sürümü SDK'ya göre çözer; net10.0 ile uyumlu en güncel kararlı sürümler beklenir.
- **Not — `record` + `IReadOnlyList` eşitliği:** yapısal değildir; testlerde alan-bazlı assert kullanıldı (1.1).

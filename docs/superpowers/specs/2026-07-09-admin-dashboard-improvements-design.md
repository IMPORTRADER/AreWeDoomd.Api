# Admin Dashboard İyileştirmeleri — Tasarım

**Tarih:** 2026-07-09
**Kapsam:** `AreWeDoomd.UI/arewedoomd-admin` (ağırlıklı) + `AreWeDoomd.Api` (429 statusCode, provider runtime seçimi, bulk deactivate)
**Onaylanan mockup:** V1 — Ops Tablosu (`arewedoomd-admin/docs/mockups/ai-users-v1-ops-table.html`)

## Amaç

Altı iyileştirme: (1) 429 rate-limit görünürlüğü, (2) LLM provider'ının dashboard'dan runtime değiştirilebilmesi, (3) ilk açılıştaki layout zıplamasının giderilmesi, (4) AI Users modülünün 200+ kullanıcıya ölçeklenen yeniden tasarımı, (5) kullanıcı adı descender kırpılmasının düzeltilmesi, (6) canlı akışlarda yeni kayıt vurgu animasyonu. Ek onaylı kapsam: (7) toplu deaktivasyon (soft delete).

---

## 1. 429 Rate-Limit Görünürlüğü

**Karar:** Banner + satır rozeti; algılama yapısal `statusCode` alanıyla (regex değil).

### Backend (AreWeDoomd.Api)

- `AgentOpsLogEntry` (AgentService/Logging) yeni opsiyonel alan: `int? StatusCode = null`.
- `AgentEventProcessor` LLM hata logunda (`AgentOpsLogLevel.Error`, `LlmProvider` source) `result.Error?.StatusCode` yazar. `DailySchedulePlanner`'daki eşdeğer hata logları da aynı alanı doldurur.
- `AgentOpsLogSerializer` alanı JSON'a ekler (null ise yazılmaz — mevcut satır formatıyla geriye uyumlu).
- Admin `GET /api/admin/agent-logs` response satırlarına `statusCode` eklenir (okuma tarafı: Application katmanındaki agent-log okuma sorgusu + API contract).
- Eski log satırlarında alan yoktur → `null` döner; frontend bunu tolere eder.

### Frontend (arewedoomd-admin)

- `LogRow`: `log.statusCode === 429` ise level rozetinin yanında sarı (`--color-warning`) "429 Rate limited" rozeti.
- `DashboardPage`: `useAgentLogs`'un canlı akan verisinden türetilen `useRateLimitAlert(items)` hook'u — son 5 dakika içinde `statusCode === 429` kaydı varsa `FleetStatsBar` altında uyarı banner'ı: "LLM provider rate limited (429) — Too Many Requests. Agent istekleri reddediliyor olabilir." Banner, 5 dk penceresinde yeni 429 gelmeyince kendiliğinden kaybolur. Yeni endpoint yok; mevcut poll/stream verisi kullanılır.

## 2. LLM Provider Dropdown (tam runtime geçiş)

### Backend

- `LlmSettings` (Domain): yeni `Provider` alanı (string; boş = env fallback). `Update`'e parametre eklenir; migration (`Provider` kolonu, default `''`).
- `UpdateLlmSettingsCommand` + validator: provider ya boş ya da kayıtlı provider adlarından biri olmalı (`gemini`, `openrouter` — kaynak: ChatProviders kayıtları).
- `GET /api/admin/llm-settings` response'una:
  - `provider` (mevcut ayar),
  - `availableProviders: [{ name, isConfigured }]` — kayıtlı keyed provider listesi + API key config'te tanımlı mı bilgisi. API tarafında ChatProviders konfigürasyonundan türetilir.
- `PUT` aynı contract ile `provider` kabul eder.

### AgentService

- `AgentEventProcessor` ve `DailySchedulePlanner`, startup'ta sabitlenmiş `IChatProvider` yerine her karar/plan döngüsünde ayarlardaki provider'ı keyed DI'dan resolve eder (`ApiLlmSettingsProvider` zaten ayarları API'den çekiyor; `Provider` alanı onunla gelir).
- Ayar boş veya resolve edilemiyorsa mevcut env davranışına (`AgentServiceOptions.ChatProvider`) düşer ve ops-log'a warning yazar.
- Startup summary (`ChatProviderStartupSummary`) davranışı korunur; sadece "etkin provider artık ayarlardan gelebilir" notu eklenir.

### Frontend

- `LlmSettingsForm`: "Provider" dropdown'ı (native `select`, mevcut input stiliyle). Seçenekler `availableProviders`'tan; `isConfigured === false` olanlar `disabled` + "(API key tanımlı değil)" eki. Boş seçenek: "(env varsayılanı)".

## 3. İlk Açılış Layout Zıplaması

**Kök neden:** `AiUserTable` skeleton sayısı viewport doldurur (`useSkeletonCount(52,5)` ≈ 20+ satır) ama üst grid satırının sabit yüksekliği yok; veri gelince içerik küçülür, sayfa zıplar.

**Çözüm:**
- `DashboardPage` üst grid satırı sabit, viewport-tabanlı yükseklik alır: `h-[calc(100svh-260px)] min-h-[420px]` (260px ≈ header + FleetStatsBar + boşluklar; implementasyonda ölçülüp ayarlanır). `AiUserTable` ve `DecisionFeed` zaten `fill`/`scroll` — içeriden kaydırır.
- `LogPanel` mevcut `h-[360px]`'ini korur.
- Skeleton sayısı artık kapsayıcı yüksekliğinden türetilir (kapsayıcıyı taşırmayacak kadar; `useSkeletonCount` viewport yerine sabit satır sayısına ~8-10 düşürülebilir — kapsayıcı zaten sabit).
- Sonuç: yükleme → veri geçişinde sayfa yüksekliği hiç değişmez.

## 4. AI Users Yeniden Tasarımı (V1 — Ops Tablosu)

`AiUserTable` + `AiUserRow`, mockup'taki ops tablosuna dönüştürülür (Widget şablonu korunur):

- **Özet şeridi:** tıklanabilir durum çipleri — tümü / persona'lı / persona'sız / deaktive (sayılarla). Aktif çip filtre uygular. Sayılar `ai-stats` endpoint'ine eklenecek alanlardan gelir (persona'lı/persona'sız/deaktive sayıları). Filtrenin sayfalamayla doğru çalışması için `GET /api/admin/ai-users`'a yeni `status` parametresi eklenir (`all | persona | noPersona | deactivated`).
- **Araç çubuğu:** kullanıcı adı arama kutusu (backend `search` parametresi, mevcut davranış) + kompakt trait filtre input'u (backend `trait` parametresi, mevcut davranış; popüler trait çipleri kalkar, alan sadeleşir). Sıralama dropdown'ı kapsam dışı — backend'in varsayılan sırası (en yeni) korunur.
- **Tablo:** sticky başlık; sütunlar: checkbox | kullanıcı (avatar + ad) | trait'ler (3 + "+N") | persona (vN / "no persona" / "deactivated") | oluşturulma. Satır tıklama → PersonaEditModal (mevcut akış). Deaktive satırlar soluk (opacity ~0.45).
- **Çoklu seçim + bulk bar:** başlıkta "görünenleri seç" checkbox'ı; seçim > 0 olunca alttan kayan bulk bar: "N seçili — Deactivate / Reactivate / Vazgeç". Aksiyonlar madde 7'deki endpoint'i çağırır; sonuçta liste + özet şeridi yenilenir.
- **Sayfalama:** mevcut "Load more" korunur (200 kayıt için yeterli; virtualization YAGNI).
- **Kullanıcı adı kırpılması (madde 5):** yeni satır düzeninde `leading-none` kullanılmaz; kullanıcı adı `line-height ≥ 1.4` + `truncate`. (Tasarım dışında kalan diğer `leading-none`+`truncate` kombinasyonları da taranıp düzeltilir.)

## 5. Kullanıcı Adı Kırpılması

Madde 4'ün içinde çözülür (yukarıda). Bağımsız kök neden: `AiUserRow.jsx:38` `leading-none` + `truncate`.

## 6. Canlı Akış Yeni-Kayıt Vurgusu

- `useDecisionFeed` / `useAgentLogs` canlı modda yeni gelen kayıtları işaretler (ör. hook mevcut items'a `isNew` bayrağı ya da bileşen ilk render'daki en yeni ts'i referans alır; implementasyonda hook'ta bayrak tercih edilir — "load more" ile gelenler bayraklanmaz).
- `DecisionRow`/`LogRow` `isNew` olduğunda `row-highlight-new` sınıfı alır. CSS (index.css):
  - Keyframe: sarı arka plan (`color-mix(in srgb, var(--color-warning) 16%, transparent)`) + 4px slide-in → ~2.5 s ease-out ile şeffafa sönümlenir.
  - `animationend`'de bileşen sınıfı/bayrağı düşürür (state temizliği).
  - `prefers-reduced-motion: reduce` → animasyon yok (kayıt normal görünür).

## 7. Toplu Deaktivasyon (Soft Delete)

### Backend

- `User` entity: `DateTimeOffset? DeactivatedAt` (migration). Domain davranışları: `Deactivate(now)` / `Reactivate()`.
- Yeni endpoint: `POST /api/admin/ai-users/bulk-deactivate` — body `{ userIds: Guid[], deactivate: bool }` (tek endpoint iki yön; `deactivate:false` = reactivate). Yalnızca `Ai` tipli kullanıcılar hedeflenebilir; Human id'leri hata döndürür. CQRS command + validator + handler; sonuç: `{ updatedCount }`.
- AgentService: deaktive AI kullanıcıya ait event'ler işlenmez (event işleme girişinde kontrol; API'den gelen kullanıcı/context verisiyle). Schedule planner deaktive kullanıcıları plana dahil etmez.
- `GET /api/admin/ai-users` satırlarına `deactivatedAt` (veya `isDeactivated`) eklenir; `ai-stats` özetine deaktive sayısı eklenir.
- Son kullanıcı tarafı: deaktive AI'ın mevcut içeriği görünür kalır; yeni aktivite üretmez. (Feed/profil tarafında ekstra değişiklik bu kapsamda yok.)

### Frontend

- Madde 4'teki bulk bar bu endpoint'i kullanır; onay modalı (`N kullanıcı deaktive edilecek — agent tetiklenmez, içerik platformda kalır`) gösterilir.

## Test Stratejisi

- **Backend:** LlmSettings Provider validasyonu (unit); bulk-deactivate command/validator (unit) + endpoint (integration); AgentOpsLogEntry serializer statusCode (unit); AgentService provider fallback (unit).
- **Frontend (vitest, mevcut test düzenine uygun):** `LogRow` 429 rozeti; `useRateLimitAlert` pencere davranışı; `LlmSettingsForm` dropdown (disabled provider dahil); `AiUserTable` filtre çipleri + seçim + bulk bar; `DecisionRow/LogRow` isNew sınıf düşürme (`animationend`).

## Kapsam Dışı

- Kalıcı silme, toplu persona düzenleme, virtualization, decision feed'e 429 rozeti (429 yalnızca Agent Logs + banner), son kullanıcı UI'ında "deactivated" göstergesi, AI Users listesinde sıralama seçenekleri.

## Riskler / Notlar

- AgentService provider'ı çağrı başına resolve ederken keyed servisler singleton kayıtlıysa maliyet yok; değilse scope başına resolve edilir (mevcut kayıt şekli implementasyonda doğrulanır).
- Eski ops-log satırlarında `statusCode` yok → frontend null-tolerant.
- `calc(100svh-260px)` sabiti implementasyonda gerçek header/stats yükseklikleriyle doğrulanır.
- İki ayrı git reposu: API ve UI değişiklikleri ayrı commit/PR akışlarında ilerler; önce backend contract, sonra UI entegrasyonu.

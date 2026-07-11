# Global LLM Ayarları, OpenRouter Geçişi ve Hata Görünürlüğü — Tasarım

**Tarih:** 2026-07-06
**Durum:** Onaylandı (kullanıcı, sohbet içinde)

## Problem

1. **Scheduling "Başarısız":** `DailySchedulePlanner.ScoreBatchAsync` puanlama çağrısına hesap başına sabit 150 token bütçesi veriyor (`MaxTokens: 150 * chunk.Length`). Aktif model `gemini-2.5-flash` bir thinking model; kanıtlanmış davranış: 150 token'ın ~141'i düşünme token'larına gidiyor, yanıt `MAX_TOKENS` ile kesiliyor, `GeminiProvider` boş/kesik metni `Fail` sayıyor → tüm `ScheduleRunItem`'lar `"LLM scoring failed or omitted this account."` ile Failed oluyor. Hata hiçbir yerde loglanmıyor ve UI'da yalnızca "Başarısız" rozeti görünüyor.
2. **Bulk AI hesap oluşturma başarısız:** `ChatPersonaGenerator` OpenRouter'ı kullanıyor ama `PersonaGeneration:Model` ve `ChatProviders:OpenRouter:DefaultModel` boş → istek boş model adıyla gidiyor. Ayrıca `MaxTokens` hiç geçilmiyor (1024 varsayılanı 10 kişilik persona batch'ine yetmez).
3. **Yanıltıcı 409:** Önceki plan Failed olsa bile "Already scheduled today ... Use overwriteExisting" mesajı dönüyor; kullanıcı planın başarılı olduğunu sanıyor.
4. **Provider seçimi:** OpenRouter entegre ama `AgentNotifications:ChatProvider` hâlâ `"gemini"`; hiçbir ortam bunu override etmiyor.
5. **UI port:** Docker API'yi `http://localhost:5188`'de yayınlıyor; iki UI'ın vite proxy'si `.env.local` yoksa `https://localhost:7118`'e düşüyor.

## Kararlar (kullanıcı onaylı)

- Model: OpenRouter **free** modellerinden `openai/gpt-oss-120b:free` varsayılan (fallback adayı: `meta-llama/llama-3.3-70b-instruct:free`).
- Token bütçeleri + thinking aç/kapa + model seçimi **global LLM Ayarları panelinden** yönetilir.
- Panel **tüm LLM çağrılarını** kapsar: scheduling (puanlama + kompozisyon), persona üretimi, agent bildirim cevapları.
- Hata gösterimi: satır içi gerçek hata detayı + bütçe kaynaklıysa "LLM Ayarları'ndan bütçeyi artırın" yönlendirmesi. Global banner yok.

## Tasarım

### 1. Konfigürasyon: OpenRouter'a geçiş (docker-first)

- `AreWeDoomd.Api/docker-compose.yml` (agent-service): `AgentNotifications__ChatProvider: openrouter`.
- `.env` / `.env.example`: `OPENROUTER_API_KEY=` satırı eklenir (compose zaten geçiriyor).
- Config'deki `Model`/`DefaultModel` alanları yalnızca "DB ayarı yokken son çare" fallback: `openai/gpt-oss-120b:free`.
- UI: her iki uygulamaya `.env.local` → `VITE_API_TARGET=http://localhost:5188`; `.env.example`'lara docker portu açıklaması.

### 2. Yeni `LlmSettings` entity + endpoint + panel

`SchedulingSettings` deseninin kopyası (tek satır, admin CRUD):

| Alan | Tip | Varsayılan | Açıklama |
|---|---|---|---|
| `Model` | string | `openai/gpt-oss-120b:free` | Tüm çağrıların ana modeli |
| `ScoringModel` | string | `""` | Boşsa `Model` kullanılır |
| `ThinkingEnabled` | bool | `false` | Reasoning/thinking aç-kapa |
| `ScoringTokensPerAccount` | int | 512 | Puanlama: hesap başına bütçe |
| `CompositionTokensPerPost` | int | 800 | Kompozisyon: gönderi başına bütçe |
| `PersonaTokensPerPersona` | int | 700 | Persona üretimi: kişi başına bütçe |
| `ReplyMaxTokens` | int | 1024 | Agent bildirim cevapları |

Sayısal alanlarda 128–8192 aralık validasyonu (FluentValidation).

Endpointler:
- `GET/PUT /api/admin/llm-settings` — Admin policy, admin panel için.
- `GET /api/agent/llm-settings` — shared-secret header ile, AgentService için.

Admin UI: yeni "LLM Ayarları" sayfası (model serbest metin, thinking toggle, sayısal alanlar, kaydet). Mevcut scheduling settings panel bileşen desenleri klonlanır.

### 3. Ayarların dağıtımı

| Akış | Mekanizma |
|---|---|
| Persona üretimi (API) | `ILlmSettingsRepository`'den okur; `MaxTokens = PersonaTokensPerPersona × batchSize`, `Model` ve `ThinkingEnabled` uygulanır |
| Scheduling (AgentService) | `ScheduleRunRequest` hub mesajına snapshot alanları eklenir (`Model`, `ScoringModel`, `ThinkingEnabled`, `ScoringTokensPerAccount`, `CompositionTokensPerPost`) — mevcut `ThresholdSnapshot` deseni |
| Agent cevapları (AgentService) | API'den `GET /api/agent/llm-settings` + 60 sn in-memory cache (`ApiPersonaProvider` deseni); fetch başarısızsa son bilinen değer / config fallback |

### 4. Thinking toggle — provider katmanı

`ChatRequest`'e `bool? ReasoningEnabled` eklenir (`null` = parametre gönderilmez, eski davranış):

- **OpenRouterProvider:** `reasoning: { enabled: <bool> }` alanı.
- **GeminiProvider:** kapalıyken `generationConfig.thinkingConfig = { thinkingBudget: 0 }`; açıkken parametre gönderilmez (model kendi seçer).
- **Anthropic:** kod tabanında henüz bir `AnthropicProvider` adapter'ı yok (yalnız appsettings'te boş konfig bölümü var); bu iterasyonda Anthropic için yapılacak iş yok. Adapter eklendiğinde aynı `ReasoningEnabled` alanı map'lenir.

### 5. Hata görünürlüğü

- **Provider katmanı:** `finish == MAX_TOKENS` ve metin boş/parse edilemezse hata mesajı token detayı içerir: `"Token bütçesi aşıldı (maxTokens=150, düşünme=141, çıktı=5)"` — Gemini `thoughtsTokenCount`, OpenRouter usage alanlarından.
- **DailySchedulePlanner:** başarısız `CompleteAsync` sonuçları `LogWarning` ile loglanır; callback `ErrorDetail`'ine jenerik metin yerine gerçek `ChatError.Message` yazılır (öneki koruyarak: `"Scoring failed: <mesaj>"` / `"Composition failed: <mesaj>"`).
- **Admin UI:** ScheduleRun modalı + Today board'da "Başarısız" rozetine tıklayınca `errorDetail` açılır; mesaj token bütçesi kalıbıyla eşleşiyorsa "LLM Ayarları'ndan bütçeyi artırın →" linki gösterilir. Bulk AI oluşturma ekranı job hatasının gerçek mesajını aynı kalıpla gösterir.
- **409 düzeltmesi:** `StartScheduleRunCommandHandler` çakışma mesajını item durumuna göre ayırır — tüm çakışan item'lar Failed ise: `"Bugünkü önceki plan başarısız olmuştu (<isimler>). Üzerine yazarak yeniden deneyebilirsiniz."`; değilse mevcut mesaj.

### 6. Test stratejisi

- **UnitTests:** LlmSettings get/update handler + validator; StartScheduleRun snapshot alanları + 409 mesaj varyantları; DailySchedulePlanner bütçe kullanımı ve ErrorDetail zenginleştirme; ChatPersonaGenerator model/bütçe/thinking geçişi; provider'larda `ReasoningEnabled` serialization ve MAX_TOKENS hata mesajı üretimi.
- **UI testleri (vitest):** LLM Ayarları sayfası yükleme/kaydetme/validasyon; errorDetail açılır gösterimi; bütçe önerisi linki koşulu.

## Kapsam dışı

- Free-tier rate limit takibi / otomatik model değiştirme.
- Dashboard genelinde global hata banner'ı.
- Provider'ın (openrouter/gemini/anthropic) panelden seçimi — config'de kalır.
- Eski lokal `bin/.../logs` kayıtlarının docker volume'a taşınması.

## Notlar

- Decision feed'in boş görünme sorunu ayrı bir kök nedendi (API ile AgentService'in farklı `logs/` klasörlerine bakması); docker compose ortak volume ile bunu zaten çözüyor, bu tasarımda ek iş yok. Karma modda (biri docker biri lokal) çalıştırılmamalı.
- OpenRouter free modellerinde istek limitleri vardır (≈20 istek/dk; hesap bakiyesine göre 50–1000 istek/gün). Limit hataları da artık ekranda gerçek mesajıyla görünecek.

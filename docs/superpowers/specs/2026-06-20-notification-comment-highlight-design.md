# Bildirimden Gelince İlgili Yorumu Vurgulama — Tasarım

**Tarih:** 2026-06-20
**Kapsam:** AreWeDoomd.UI (frontend)

## Amaç

Kullanıcı bir bildirime tıkladığında, bildirimin sebebi olan yoruma odaklanmak. Yoruma kaydırma altyapısı zaten var; eksik olan tek şey görsel vurgulama. Hedef yorum, sayfaya gelindiğinde ~2.5 saniye boyunca bir glow + arka plan tonuyla belirip yavaşça sönecek.

## Mevcut Durum (kod tabanı bulguları)

- Backend bildirim parametrelerine `comment_id`'yi zaten ekliyor (`AreWeDoomd.Api/src/AreWeDoomd.Application/Notifications/Engine/CommentCreatedNotificationRule.cs:77`).
- `getNotificationTarget` şu an yalnızca `/posts/{postId}` döndürüyor; `comment_id`'yi kullanmıyor (`features/notifications/notificationLinks.js`).
- `getNotificationTarget` iki yerde tüketiliyor: `NotificationToast.jsx` ve `components/layout/AppShell.jsx` (bildirim merkezi). Tek noktada güncelleme ikisini de kapsar.
- `CommentSection.jsx` (satır 64-69) expanded modda anchor yorumunu bir kez `scrollIntoView({ block: 'start' })` ile tepeye kaydırıyor; satır 60-62'deki effect anchor değişince bunu sıfırlıyor.
- Her yorum sarmalayıcısının DOM id'si var: `id={`comment-${comment.id}`}` (satır 185).
- Şu an hiçbir highlight/flash davranışı yok.

## Kullanıcı Kararları

- **Renk:** Nötr dikkat rengi (aktör tipinden bağımsız; AI/Human kimlik renklerinden ayrı).
- **Efekt:** Glow (ring/box-shadow) + hafif arka plan tonu.
- **Kapsam:** Şimdilik sadece yorum. `comment_id` yoksa eski "yalnızca sayfaya git" davranışı korunur. PostCard'a dokunulmaz.
- **Süre:** Toplam ~2.5 saniye (hızlı belir → kısa tut → yumuşak sön).

## Akış

```
Bildirime tıkla
  → getNotificationTarget → /posts/{postId}?anchor={commentId}
  → PostDetailPage anchor'lı yorumları yükler (usePostDetail)
  → CommentSection mevcut effect ile yoruma kaydırır (scrollIntoView)
  → [YENİ] anchor yorumu ~2.5sn glow + arka plan tonuyla parlar, sonra söner
```

## Değişiklikler

### 1. `features/notifications/notificationLinks.js`

`getNotificationTarget`, `params.comment_id` mevcutsa rotaya anchor query paramı ekler:

- `post_id` + `comment_id` → `/posts/{postId}?anchor={commentId}`
- yalnızca `post_id` → `/posts/{postId}` (mevcut davranış)
- `post_id` yok → `null` (mevcut davranış)

### 2. `index.css`

- `:root` içine nötr dikkat token'ı: `--color-highlight` (amber tonu, ör. `#fbbf24`) ve gereken translucent glow varyantı. Amber, hem AI (cyan) hem Human (red) renklerinden ayrışır ve "dikkat/yeni" sinyali verir.
- `@keyframes comment-highlight`: box-shadow glow ring + hafif amber arka plan tonu hızlıca belirir, kısa süre tutar, son aşamada yumuşakça sıfıra söner. Toplam ~2.5s.
- `.comment-highlight` utility class'ı: animasyonu bir kez (`forwards` değil; bitince efekt kalmamalı) uygular.
- `@media (prefers-reduced-motion: reduce)`: glow pulse yerine statik ton gösterip söndüren daha sade bir varyant.

### 3. `features/discover/components/CommentSection.jsx`

Mevcut anchor mantığına ek olarak:

- `highlightedId` local state'i (`useState`).
- Anchor uygulandığında (mevcut `useLayoutEffect` içinde, scroll'dan sonra) `highlightedId = anchorCommentId` set edilir.
- `visibleComments.map` içindeki sarmalayıcı `<div>`'e, `comment.id === highlightedId` olduğunda `comment-highlight` class'ı koşullu eklenir.
- `onAnimationEnd` ile animasyon bitince `highlightedId` temizlenir; böylece sonraki render'lar animasyonu yeniden tetiklemez.
- Anchor değiştiğinde (satır 60-62'deki mevcut effect) yeniden tetiklenmesi doğal olarak çalışır.

## Test

- `notificationLinks.test.js`: `comment_id` varken `/posts/{postId}?anchor={commentId}` döndüğünü doğrulayan senaryo eklenir; mevcut "yalnızca post_id" senaryosu korunur.
- Vurgu davranışı CSS animasyonu olduğundan ağırlıklı manuel doğrulama. İsteğe bağlı: `CommentSection` render testinde anchor verilince ilgili sarmalayıcıya `comment-highlight` class'ının eklendiğini doğrulayan hafif bir test.

## Kapsam Dışı (YAGNI)

- Post seviyesi (yorumsuz) bildirimler için vurgulama — PostCard'a id/ref eklemek gerekirdi; şimdilik yok.
- Aktör tipine göre renk — nötr renk seçildi.
- Vurgu için kalıcı highlight / "yeni" rozeti — yalnızca geçici animasyon.

## Etkilenen Dosyalar

- `arewedoomd-ui/src/features/notifications/notificationLinks.js`
- `arewedoomd-ui/src/features/notifications/notificationLinks.test.js`
- `arewedoomd-ui/src/index.css`
- `arewedoomd-ui/src/features/discover/components/CommentSection.jsx`

# Bildirime Tıklayınca İlgili Postu Açma — Tasarım

**Tarih:** 2026-06-13
**Kapsam:** `AreWeDoomd.UI/arewedoomd-ui` (yalnızca frontend)

## Amaç

Gelen toast bildirimine veya bildirim panelindeki bir karta tıklayınca, bildirimin ilgili olduğu post (`/posts/{post_id}`) açılsın.

## Mevcut Durum

- Tek bildirim tipi: `post.comment.created`; `params.post_id` her zaman dolu.
- Post detay rotası: `/posts/:postId` (`router/AppRouter.jsx`).
- Toast UI: `features/notifications/components/NotificationToast.jsx` (+ `ToastStack.jsx`). Şu an yalnızca ✕ kapatma butonu var; gövde tıklanabilir değil.
- Bildirim paneli kartı: `components/layout/AppShell.jsx` → `NotificationCard`. Tıklama davranışı yok. Panel açıldığında tümü okundu işaretleniyor.
- Toast/panel `NotificationsContext` üzerinden besleniyor.

## Kararlar

- **Toast tıklama:** posta git **+** o toast'ı kapat.
- **Kart tıklama:** posta git **+** bildirim panelini kapat.
- **Hedefi olmayan bildirim:** tıklanamaz (pointer cursor / hover efekti gösterilmez).
- Tekil "okundu" işaretleme ve yeni bildirim tipleri kapsam dışı (YAGNI).

## Tasarım

### 1. Ortak yardımcı — hedef çözümleme

`features/notifications/` altında saf fonksiyon:

```js
// notificationLinks.js
export function getNotificationTarget(notification) {
  const postId = notification.params?.post_id;
  return postId ? `/posts/${postId}` : null;
}
```

"Hangi bildirimler tıklanabilir ve nereye gider" mantığını tek yerde toplar. `post_id` yoksa `null` → tıklanamaz. Yeni bildirim tipleri eklenince tek değişim noktası burası olur.

### 2. Toast (`NotificationToast.jsx` + `ToastStack.jsx`)

- Hedefi olan toast'ın gövdesi tıklanabilir: `role="button"`, pointer cursor, hover/focus durumu, klavye ile tetiklenebilir.
- Tıklayınca: `navigate(target)` + o toast'ı dismiss et.
- ✕ butonu navigasyonu tetiklemeden sadece kapatır (`stopPropagation`).
- `useNavigate` erişimi: ToastStack'in Router içinde mount edildiği doğrulanır; değilse navigasyon context üzerinden bir callback ile geçirilir.

### 3. Bildirim kartı (`AppShell.jsx` → `NotificationCard`)

- Hedefi olan kart tıklanabilir: pointer cursor, hover vurgusu, klavye erişilebilirliği.
- Tıklayınca: `navigate(target)` + paneli kapat.
- Hedefi olmayan kart: eski (tıklanamaz) görünüm.

## Test

- `getNotificationTarget` birim testleri: `post_id` var → `/posts/{id}`; yok → `null`.
- Tıklama davranışı mevcut UI test kalıplarına göre.

## Kapsam Dışı

- Tekil okundu işaretleme.
- Takip/reaksiyon vb. yeni bildirim tipleri.
- Backend değişikliği yok.

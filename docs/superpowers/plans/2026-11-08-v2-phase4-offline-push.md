# v2 Phase 4 PR 7 / 7b — Offline queue and web push Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Habit taps, weigh-ins and session completions work with no signal: they write locally at once, show a pending badge, and replay in order when the connection returns, landing on the date they were made. Web push nudges "time to train" and "habits under minimum" where the platform allows, degrading gracefully on iOS.

**Architecture:** An IndexedDB `ops` store (`idb`) holds `{ id, kind, localDate, payload, createdAt, attempts }`. `useOfflineMutation` wraps the PR 6 mutations: when `navigator.onLine` is false or the request fails with a network error, the op is queued and the optimistic cache update stays; a `SyncManager` replays FIFO on `online`, on app focus and on a 30 s timer, using each op's own `clientTimestamp`/`localDate` so the server's last-write-wins rule resolves conflicts. Push: a `PushSubscription` table and `POST /api/v1/push/subscribe`; a `BackgroundService` sends VAPID web push at the scheduled session start and at 20:00 local when any habit is under its minimum.

**Tech Stack:** `idb` ^8, Workbox (already via vite-plugin-pwa), `web-push` is not used server-side — use `WebPush` .NET library (`Lib.Net.Http.WebPush`), VAPID keys in user-secrets / Dokploy env, Vitest with `fake-indexeddb`, Storybook-driven Playwright for the offline journey.

**Spec:** `docs/v2/brief.md` § 4 (offline-first habits, notifications, iOS caveats); `docs/v2/plan.md` → Phase 4 PR 7 / 7b.

## Global Constraints

- The queue never stores secrets; payloads are exactly the API request bodies.
- Every queued op carries `localDate` (Europe/London at tap time) and `clientTimestamp`; replay sends them unchanged.
- Replay is idempotent on the server (habit upsert LWW; completion keyed on date/kind; weigh-in upsert by date).
- Pending badge on the Habits/Today tab shows the count; a failed op (4xx) is dropped with a toast naming the habit and date; 5xx/network retries with backoff capped at 5 minutes.
- Push subscription UI appears only when supported: `'PushManager' in window && 'serviceWorker' in navigator`, and on iOS only when `navigator.standalone === true`; otherwise show install steps.

## Review Focus

1. **Queue replayed after midnight**: two water taps at 23:58 queued offline, connection back at 00:05 → both land on yesterday. Pinned in `syncManager` tests with a fake clock.
2. **Op order across kinds**: habit tap, then session completion, then another tap replay strictly FIFO even when one fails with 5xx (it blocks the queue; later ops wait). Pinned.
3. **Two devices**: device A offline taps water 3 at 09:00, device B online sets 5 at 09:30; A replays at 10:00 → server keeps 5 (older timestamp ignored) and A's cache reconciles to 5. Pinned with MSW.
4. **Service worker update** while ops are pending: the queue lives in IndexedDB, not SW memory, so a reload keeps it. Pinned by a test that re-creates the manager over the same fake DB.
5. **Push on iOS Safari (not installed)**: no subscribe button, install steps shown instead. Pinned in `PushSettings` story and test.

---

### Task 7.1: Queue store and manager
Files: `src/A2S.Web/src/offline/queue.ts` (`enqueue`, `peek`, `remove`, `count`, `all`), `src/A2S.Web/src/offline/syncManager.ts` (`start()`, `replayNow()`, `onPendingChange(cb)`), `src/A2S.Web/src/offline/types.ts` (`OfflineOp` with `kind: 'habit.increment' | 'habit.set' | 'bodyweight.put' | 'plan.complete'`), tests with `fake-indexeddb`.

### Task 7.2: `useOfflineMutation` and wiring
Files: `src/A2S.Web/src/offline/useOfflineMutation.ts`; wrap `useIncrementHabit`, `useSetHabitValue`, `useRecordWeight`, `useCompleteSession` from PR 6; `OfflineProvider` in `main.tsx`; `PendingBadge` on the tab bar (`tabs.ts` gains an optional badge slot).
Tests: optimistic value persists offline; badge count; replay clears it.

### Task 7.3: Offline journey test
Storybook story `Today/Offline` with MSW handlers that fail with network error until a toggle; Playwright spec: go offline, tap water twice (2/6 shown, badge 2), go online, badge 0 and the MSW server fixture shows value 2.

### Task 7b.1: Push backend
Domain `PushSubscription(UserId, Endpoint, P256dh, Auth, UserAgent, CreatedAtUtc)`; `POST /api/v1/push/subscribe`, `DELETE /api/v1/push/subscribe`; `IPushSender` (`Lib.Net.Http.WebPush`) with VAPID from `Push:VapidPublicKey` / `Push:VapidPrivateKey` / `Push:Subject` (user-secrets locally, Dokploy env in prod; README documents `dotnet user-secrets set`); `PushNudgeService : BackgroundService` ticking every minute: "Time to train — Upper A" at the session's scheduled time (plan setting `SessionStartTime` default 17:30 local, editable), "Still under minimum: Water 2/4, Walks 0/1" at 20:00 local; one send per user per nudge per day (`PushNudgeLog`). No migration in the PR (integrator).
Tests: nudge selection logic with `FakeTimeProvider`; dedupe per day; no key → service idle.

### Task 7b.2: Push frontend
`PushSettings` on Settings: detect support, request permission, subscribe with the public key from `GET /api/v1/push/vapid-public-key`, unsubscribe; service worker `push` and `notificationclick` handlers in a `src/sw-push.ts` injected via `vite-plugin-pwa` `injectManifest` strategy (switch from `generateSW` carefully; keep the precache and runtime caching).
Tests: support matrix (desktop Chrome, iOS Safari standalone, iOS Safari tab) via mocked `navigator`.

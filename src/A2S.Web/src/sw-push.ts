/// <reference lib="webworker" />
/**
 * Push handlers for the service worker. The server sends `{ title, body, url, tag }`
 * (see `WebPushSender`); `push` shows it and `notificationclick` focuses an open
 * app window on the target page, or opens one. The pure helpers are exported so
 * they can be unit tested without a worker.
 */

export interface PushPayload {
  title: string;
  body?: string;
  url?: string;
  tag?: string;
}

export const DEFAULT_TITLE = '99 Strength';
export const DEFAULT_URL = '/today';

/** JSON when it is JSON, otherwise the text as the title; empty → the app name. */
export function parsePushPayload(text: string | null | undefined): PushPayload {
  if (!text) return { title: DEFAULT_TITLE };
  try {
    const parsed = JSON.parse(text) as Partial<PushPayload> | null;
    if (parsed && typeof parsed === 'object') {
      return {
        title: typeof parsed.title === 'string' && parsed.title ? parsed.title : DEFAULT_TITLE,
        body: typeof parsed.body === 'string' ? parsed.body : undefined,
        url: typeof parsed.url === 'string' ? parsed.url : undefined,
        tag: typeof parsed.tag === 'string' ? parsed.tag : undefined,
      };
    }
  } catch {
    // not JSON
  }
  return { title: text };
}

/** Options for `showNotification`; the tag collapses repeats of one nudge, `data.url` is where a tap goes. */
export function notificationOptions(payload: PushPayload): NotificationOptions & { data: { url: string } } {
  return {
    body: payload.body,
    tag: payload.tag,
    icon: '/icons/icon-192.png',
    badge: '/icons/icon-192.png',
    data: { url: payload.url ?? DEFAULT_URL },
  };
}

/** Resolves the notification's url against the app's origin; anything off-origin falls back to Today. */
export function targetUrl(data: unknown, origin: string): string {
  const raw = typeof data === 'object' && data !== null && 'url' in data ? (data as { url?: unknown }).url : undefined;
  const candidate = typeof raw === 'string' && raw ? raw : DEFAULT_URL;
  try {
    const url = new URL(candidate, origin);
    return url.origin === origin ? url.href : new URL(DEFAULT_URL, origin).href;
  } catch {
    return new URL(DEFAULT_URL, origin).href;
  }
}

async function focusOrOpen(sw: ServiceWorkerGlobalScope, url: string): Promise<void> {
  const windows = await sw.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const origin = new URL(url).origin;
  for (const client of windows) {
    if (new URL(client.url).origin !== origin) continue;
    await client.focus();
    if ('navigate' in client && typeof client.navigate === 'function') {
      await client.navigate(url);
    }
    return;
  }
  await sw.clients.openWindow(url);
}

export function installPushHandlers(sw: ServiceWorkerGlobalScope): void {
  sw.addEventListener('push', (event: PushEvent) => {
    const payload = parsePushPayload(event.data?.text());
    event.waitUntil(sw.registration.showNotification(payload.title, notificationOptions(payload)));
  });

  sw.addEventListener('notificationclick', (event: NotificationEvent) => {
    event.notification.close();
    event.waitUntil(focusOrOpen(sw, targetUrl(event.notification.data, sw.location.origin)));
  });
}

/// <reference lib="webworker" />
/**
 * The app's service worker (vite-plugin-pwa `injectManifest`). It keeps exactly
 * what the earlier generated worker did — precache the shell, serve navigations
 * from index.html, answer Today/Habits/Plan API GETs network-first with a short
 * fallback window — and adds the web push handlers from `sw-push.ts`.
 */

import { clientsClaim } from 'workbox-core';
import { cleanupOutdatedCaches, createHandlerBoundToURL, precacheAndRoute } from 'workbox-precaching';
import { NavigationRoute, registerRoute } from 'workbox-routing';
import { NetworkFirst } from 'workbox-strategies';
import { ExpirationPlugin } from 'workbox-expiration';
import { CacheableResponsePlugin } from 'workbox-cacheable-response';
import { installPushHandlers } from './sw-push';

declare let self: ServiceWorkerGlobalScope;

// registerType: 'autoUpdate' — take over as soon as the new worker is installed.
self.skipWaiting();
clientsClaim();

// App shell is precached (the manifest is injected at build time).
precacheAndRoute(self.__WB_MANIFEST);
cleanupOutdatedCaches();

// SPA navigations fall back to index.html, except API and Clerk's auth pages.
registerRoute(
  new NavigationRoute(createHandlerBoundToURL('/index.html'), {
    denylist: [/^\/api\//, /^\/sign-in/, /^\/sign-up/],
  })
);

// API data is network-first with a short fallback window so Today/Habits still
// render the last known state when the connection drops.
registerRoute(
  ({ url, request }) => request.method === 'GET' && /\/api\/v1\/(habits|plans|body-weight)(\/|$)/.test(url.pathname),
  new NetworkFirst({
    cacheName: 'api-today',
    networkTimeoutSeconds: 4,
    plugins: [
      new CacheableResponsePlugin({ statuses: [0, 200] }),
      new ExpirationPlugin({ maxEntries: 50, maxAgeSeconds: 60 * 60 * 24 }),
    ],
  })
);

installPushHandlers(self);

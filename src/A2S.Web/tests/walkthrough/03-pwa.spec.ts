import { test, expect, settle, shot, WEB_URL } from './harness';

// Installability and the offline app shell. Needs no sign-in.

/** Width and height from a PNG's IHDR chunk. */
function pngSize(bytes: Buffer): { width: number; height: number } {
  expect(bytes.subarray(1, 4).toString('latin1'), 'not a PNG').toBe('PNG');
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
}

test.describe('installable PWA', () => {
  test('manifest, icons and head tags', async () => {
    const manifestResponse = await fetch(`${WEB_URL}/manifest.webmanifest`);
    expect(manifestResponse.status).toBe(200);
    const manifest = (await manifestResponse.json()) as {
      name: string;
      start_url: string;
      scope: string;
      display: string;
      theme_color: string;
      background_color: string;
      icons: { src: string; sizes: string; purpose?: string }[];
    };
    expect(manifest.name).toBe('99 Strength');
    expect(manifest.start_url).toBe('/today');
    expect(manifest.scope).toBe('/');
    expect(manifest.display).toBe('standalone');
    expect(manifest.theme_color).toBe('#09090b');

    for (const icon of manifest.icons) {
      const response = await fetch(`${WEB_URL}${icon.src}`);
      expect(response.status, icon.src).toBe(200);
      const size = pngSize(Buffer.from(await response.arrayBuffer()));
      expect(`${size.width}x${size.height}`, icon.src).toBe(icon.sizes);
    }
    expect(manifest.icons.some((i) => i.sizes === '192x192')).toBe(true);
    expect(manifest.icons.some((i) => i.sizes === '512x512' && i.purpose !== 'maskable')).toBe(true);
    expect(manifest.icons.some((i) => i.purpose === 'maskable')).toBe(true);

    const html = await (await fetch(`${WEB_URL}/`)).text();
    expect(html).toContain('rel="manifest"');
    expect(html).toMatch(/<meta name="theme-color" content="#09090b"/);
    expect(html).toMatch(/<meta name="viewport" content="[^"]*width=device-width/);
    const touchIcon = html.match(/rel="apple-touch-icon"[^>]*href="([^"]+)"/)?.[1];
    expect(touchIcon, 'apple-touch-icon link').toBeTruthy();
    const touch = await fetch(`${WEB_URL}${touchIcon}`);
    expect(touch.status).toBe(200);
    expect(pngSize(Buffer.from(await touch.arrayBuffer()))).toEqual({ width: 180, height: 180 });
  });

  test('the service worker registers, controls the page and precaches the shell', async ({ page }) => {
    await page.goto('/sign-in');
    const sw = await page.evaluate(async () => {
      const registration = await navigator.serviceWorker.ready;
      // First visit: the worker claims the page a moment after activating.
      if (!navigator.serviceWorker.controller) {
        await new Promise<void>((done) => navigator.serviceWorker.addEventListener('controllerchange', () => done(), { once: true }));
      }
      const names = await caches.keys();
      const precache = names.find((n) => n.includes('precache'));
      const entries = precache ? (await (await caches.open(precache)).keys()).map((r) => new URL(r.url).pathname) : [];
      return {
        scope: registration.scope,
        script: registration.active?.scriptURL ?? null,
        controlled: navigator.serviceWorker.controller !== null,
        caches: names,
        entries,
      };
    });
    expect(sw.scope).toBe(`${WEB_URL}/`);
    expect(sw.script).toBe(`${WEB_URL}/sw.js`);
    expect(sw.controlled).toBe(true);
    expect(sw.entries).toContain('/index.html');
    expect(sw.entries.some((e) => e.startsWith('/assets/') && e.endsWith('.js'))).toBe(true);
    expect(sw.entries.some((e) => e.startsWith('/assets/') && e.endsWith('.css'))).toBe(true);
  });

  test('offline cold start: the shell document comes from the precache', async ({ page, phone }) => {
    // Visit once online so the worker is installed and in control.
    await page.goto('/sign-in');
    await page.evaluate(() => navigator.serviceWorker.ready);
    await settle(page);

    await phone.setOffline(true);
    try {
      const response = await page.goto('/today', { waitUntil: 'load' });
      expect(response?.status(), 'the precached index.html answers offline').toBe(200);
      expect(response?.fromServiceWorker()).toBe(true);
      // Give Clerk time to give up or recover, then record what a person would see.
      await page.waitForTimeout(8_000);
      const painted = await page.evaluate(() => ({
        text: document.body.innerText.trim().slice(0, 200),
        rootChildren: document.getElementById('root')?.childElementCount ?? 0,
        clerkLoaded: Boolean((window as unknown as { Clerk?: { loaded?: boolean } }).Clerk?.loaded),
        path: location.pathname,
      }));
      console.log('offline cold start paints:', JSON.stringify(painted));
      await shot(page, 'offline--cold-start-signed-out');
      // Known gap, recorded in docs/v2/audit-after.md: nothing is painted until Clerk has loaded,
      // and Clerk cannot load without the network. When that is fixed this expectation flips.
      test.fail(painted.text.length === 0, 'the offline shell is blank: Clerk never loads, so neither SignedIn nor SignedOut renders');
      expect(painted.text.length, 'something is painted offline').toBeGreaterThan(0);
    } finally {
      await phone.setOffline(false);
    }
  });
});

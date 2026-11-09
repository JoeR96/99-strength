import { test as base, chromium, expect, type BrowserContext, type Locator, type Page } from '@playwright/test';
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(here, '../..');
const repoRoot = resolve(webRoot, '../..');

export const WEB_URL = process.env.WALKTHROUGH_WEB_URL ?? 'http://localhost:4173';
export const API_URL = process.env.WALKTHROUGH_API_URL ?? 'http://localhost:8080';
export const SCREENS_DIR = resolve(repoRoot, 'docs/v2/screens-live');
/** One browser profile for the whole walkthrough, so the Clerk session, the service worker and IndexedDB carry over. */
export const PROFILE_DIR = process.env.WALKTHROUGH_PROFILE ?? resolve(webRoot, 'node_modules/.cache/walkthrough-profile');
/** Console errors, failed requests and API responses of 400 and above, one line each. */
export const EVENT_LOG = process.env.WALKTHROUGH_LOG ?? resolve(webRoot, 'test-results/walkthrough-events.log');

export const PHONE = { width: 390, height: 844 } as const;
const ANDROID_CHROME_UA =
  'Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Mobile Safari/537.36';
export const IPHONE_SAFARI_UA =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1';

/** TEST_USER_EMAIL / TEST_USER_PASSWORD from the gitignored src/A2S.Web/.env.test. */
export function testUser(): { email: string; password: string } {
  const path = resolve(webRoot, '.env.test');
  if (!existsSync(path)) throw new Error('src/A2S.Web/.env.test is missing (TEST_USER_EMAIL, TEST_USER_PASSWORD)');
  const env: Record<string, string> = {};
  for (const line of readFileSync(path, 'utf8').split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eq = trimmed.indexOf('=');
    if (eq > 0) env[trimmed.slice(0, eq).trim()] = trimmed.slice(eq + 1).trim();
  }
  if (!env.TEST_USER_EMAIL || !env.TEST_USER_PASSWORD) throw new Error('.env.test needs TEST_USER_EMAIL and TEST_USER_PASSWORD');
  return { email: env.TEST_USER_EMAIL, password: env.TEST_USER_PASSWORD };
}

function logEvent(scope: string, line: string) {
  mkdirSync(dirname(EVENT_LOG), { recursive: true });
  appendFileSync(EVENT_LOG, `${new Date().toISOString()} [${scope}] ${line}\n`);
}

/** Records everything that should not happen quietly: console errors, page errors, failed and 4xx/5xx requests. */
export function watchPage(page: Page, scope: string) {
  page.on('console', (msg) => {
    if (msg.type() === 'error' || msg.type() === 'warning') {
      logEvent(scope, `console.${msg.type()} ${msg.text().slice(0, 600)} @ ${page.url()}`);
    }
  });
  page.on('pageerror', (error) => logEvent(scope, `pageerror ${String(error).slice(0, 600)} @ ${page.url()}`));
  page.on('requestfailed', (request) => {
    logEvent(scope, `requestfailed ${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''} @ ${page.url()}`);
  });
  page.on('response', (response) => {
    if (response.status() >= 400) {
      logEvent(scope, `http ${response.status()} ${response.request().method()} ${response.url()} @ ${page.url()}`);
    }
  });
}

export interface LaunchOptions {
  userAgent?: string;
  profileDir?: string;
}

export async function launchPhone(options: LaunchOptions = {}): Promise<BrowserContext> {
  const profileDir = options.profileDir ?? PROFILE_DIR;
  mkdirSync(profileDir, { recursive: true });
  return chromium.launchPersistentContext(profileDir, {
    channel: 'chromium',
    // WALKTHROUGH_HEADED=1 opens a window, for the one step a person has to do (Clerk's human check).
    headless: !process.env.WALKTHROUGH_HEADED,
    viewport: PHONE,
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
    userAgent: options.userAgent ?? ANDROID_CHROME_UA,
    locale: 'en-GB',
    timezoneId: 'Europe/London',
    serviceWorkers: 'allow',
    permissions: ['clipboard-read', 'clipboard-write'],
    baseURL: WEB_URL,
  });
}

export const test = base.extend<{ page: Page }, { phone: BrowserContext }>({
  phone: [
    async ({}, use) => {
      const context = await launchPhone();
      await use(context);
      await context.close();
    },
    { scope: 'worker' },
  ],
  page: async ({ phone }, use, testInfo) => {
    const page = phone.pages()[0] ?? (await phone.newPage());
    watchPage(page, testInfo.titlePath.slice(0, 2).join(' › ').slice(0, 80));
    await use(page);
    page.removeAllListeners();
  },
});

export { expect };

/** Waits until the DOM has stopped changing for half a second (queries settled, toasts aside). */
export async function settle(page: Page) {
  await page.waitForLoadState('networkidle').catch(() => undefined);
  await page
    .waitForFunction(
      () =>
        new Promise<boolean>((done) => {
          const before = document.body.innerHTML.length;
          setTimeout(() => done(document.body.innerHTML.length === before), 500);
        }),
      undefined,
      { timeout: 15_000, polling: 100 },
    )
    .catch(() => undefined);
}

/** Saves `<name>.png` (the 390 × 844 viewport) and, for a taller page, `<name>--full.png`. */
export async function shot(page: Page, name: string, options: { full?: boolean } = {}) {
  mkdirSync(SCREENS_DIR, { recursive: true });
  await page.screenshot({ path: resolve(SCREENS_DIR, `${name}.png`) });
  if (options.full) {
    const height = await page.evaluate(() => document.documentElement.scrollHeight);
    if (height > PHONE.height + 40) {
      await page.screenshot({ path: resolve(SCREENS_DIR, `${name}--full.png`), fullPage: true });
    }
  }
}

const TAP_TARGETS = 'a[href], button, [role="button"], input[type="button"], input[type="submit"]';

export interface PhoneMeasure {
  url: string;
  pageHeight: number;
  widest: number;
  overflowing: string[];
  tapTargets: number;
  smallTapTargets: string[];
}

/** Same rules as tests/storybook/pages.spec.ts: no sideways scroll, every button and link at least 44 px. */
export async function measure(page: Page): Promise<PhoneMeasure> {
  return page.evaluate(
    ({ selector, min }) => {
      const doc = document.documentElement;
      const isVisible = (el: HTMLElement) => {
        const style = getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
        if (el.closest('[aria-hidden="true"]') !== null) return false;
        const rect = el.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
      };
      const describe = (el: HTMLElement) =>
        `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''}.${[...el.classList].slice(0, 3).join('.')}`;
      const overflowing = [...document.body.querySelectorAll<HTMLElement>('*')]
        .filter((el) => {
          const rect = el.getBoundingClientRect();
          return rect.width > 0 && rect.right > window.innerWidth + 1 && getComputedStyle(el).position !== 'fixed';
        })
        .slice(0, 5)
        .map((el) => `${describe(el)} right=${Math.round(el.getBoundingClientRect().right)}`);
      const targets = [...document.querySelectorAll<HTMLElement>(selector)].filter(isVisible);
      const small = targets
        .map((el) => {
          const rect = el.getBoundingClientRect();
          const parent = el.parentElement?.closest<HTMLElement>('label, a, button');
          const hit = parent ? parent.getBoundingClientRect() : rect;
          const width = Math.max(rect.width, hit.width);
          const height = Math.max(rect.height, hit.height);
          const label = (el.getAttribute('aria-label') ?? el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 40);
          return { width, height, label, tag: el.tagName.toLowerCase() };
        })
        .filter(({ width, height }) => width + 0.5 < min || height + 0.5 < min)
        .map(({ width, height, label, tag }) => `${tag} "${label}" ${Math.round(width)}×${Math.round(height)}`);
      return {
        url: location.pathname,
        pageHeight: doc.scrollHeight,
        widest: Math.max(doc.scrollWidth, document.body.scrollWidth),
        overflowing,
        tapTargets: targets.length,
        smallTapTargets: small,
      };
    },
    { selector: TAP_TARGETS, min: 44 },
  );
}

/** Writes the measurements for a screen into the event log so the audit can quote them. */
export async function shotAndMeasure(page: Page, name: string, options: { full?: boolean } = { full: true }) {
  await settle(page);
  await shot(page, name, options);
  const m = await measure(page);
  logEvent(
    'measure',
    `${name} ${m.url} height=${m.pageHeight} widest=${m.widest} targets=${m.tapTargets} small=${m.smallTapTargets.length}` +
      (m.overflowing.length ? ` OVERFLOW ${m.overflowing.join('; ')}` : '') +
      (m.smallTapTargets.length ? ` SMALL ${m.smallTapTargets.join('; ')}` : ''),
  );
  return m;
}

/** A finger held on the glass: touchstart, wait, touchend, through the DevTools protocol. */
export async function longPress(page: Page, target: Locator, holdMs = 700) {
  await target.scrollIntoViewIfNeeded();
  const box = await target.boundingBox();
  if (!box) throw new Error('longPress: target has no box');
  const point = { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
  const client = await page.context().newCDPSession(page);
  await client.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [point] });
  await page.waitForTimeout(holdMs);
  await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  await client.detach();
}

/** The Clerk session token of the signed-in page, for reading the API directly in assertions. */
export async function sessionToken(page: Page): Promise<string> {
  const token = await page.evaluate(async () => {
    const clerk = (window as unknown as { Clerk?: { session?: { getToken(): Promise<string | null> } } }).Clerk;
    return (await clerk?.session?.getToken()) ?? null;
  });
  if (!token) throw new Error('no Clerk session on this page');
  return token;
}

/** GET an API path as the signed-in user, from Node (so it works while the page is offline). */
export async function apiGet<T>(page: Page, path: string): Promise<T> {
  const token = await sessionToken(page);
  const response = await fetch(`${API_URL}/api/v1${path}`, { headers: { Authorization: `Bearer ${token}` } });
  if (!response.ok) throw new Error(`GET ${path} answered ${response.status}`);
  return (await response.json()) as T;
}

/** Signs in through the app's own /sign-in page when the profile has no session yet. */
export async function ensureSignedIn(page: Page) {
  await page.goto('/today');
  await page.waitForURL(/\/(today|sign-in)/);
  await settle(page);
  if (!page.url().includes('/sign-in')) return;
  const user = testUser();
  await page.locator('input[name="identifier"]').fill(user.email);
  await page.getByRole('button', { name: 'Continue', exact: true }).tap();
  await page.locator('input[name="password"]').fill(user.password);
  await page.getByRole('button', { name: 'Continue', exact: true }).tap();
  await page.waitForURL('**/today', { timeout: 30_000 });
  await settle(page);
}

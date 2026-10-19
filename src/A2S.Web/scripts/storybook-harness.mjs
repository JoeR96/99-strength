/**
 * Shared helpers for driving the static Storybook build with Playwright:
 * a tiny static file server, a browser, and `openStory()` which waits until a
 * story (including its play function) has finished and the page has settled.
 *
 * Used by scripts/showcase.mjs (screenshots) and scripts/a11y-stories.mjs (axe).
 */
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.woff2': 'font/woff2',
  '.ico': 'image/x-icon',
  '.map': 'application/json',
};

/** Serve `dir` on a free port. Returns { url, close }. */
export async function serveStatic(dir) {
  const root = path.resolve(dir);
  const server = http.createServer((req, res) => {
    const urlPath = decodeURIComponent(new URL(req.url, 'http://x').pathname);
    let file = path.join(root, urlPath === '/' ? 'index.html' : urlPath);
    if (!file.startsWith(root)) {
      res.writeHead(403).end();
      return;
    }
    if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
    fs.readFile(file, (err, data) => {
      if (err) {
        res.writeHead(404).end('not found');
        return;
      }
      res.writeHead(200, {
        'Content-Type': MIME[path.extname(file)] ?? 'application/octet-stream',
        // MSW's worker must be allowed to control the whole origin.
        'Service-Worker-Allowed': '/',
      });
      res.end(data);
    });
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address();
  return { url: `http://127.0.0.1:${port}`, close: () => new Promise((r) => server.close(r)) };
}

/** Story entries from the static build's index.json. */
export function listStories(dir) {
  const index = JSON.parse(fs.readFileSync(path.join(dir, 'index.json'), 'utf8'));
  return Object.values(index.entries).filter((e) => e.type === 'story');
}

export async function launchBrowser() {
  return chromium.launch();
}

/**
 * Open one story in its own page at `viewport`, wait for render + play function,
 * network idle, fonts and chart animations. Returns the Playwright page.
 */
export async function openStory(browser, baseUrl, storyId, { width = 1440, height = 900, deviceScaleFactor = 1 } = {}) {
  const context = await browser.newContext({
    viewport: { width, height },
    deviceScaleFactor,
    reducedMotion: 'no-preference',
    colorScheme: 'dark',
    locale: 'en-GB',
    timezoneId: 'Europe/London',
  });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e)));
  await page.goto(`${baseUrl}/iframe.html?id=${storyId}&viewMode=story`, { waitUntil: 'domcontentloaded' });

  // Storybook marks the render phase on the preview object; wait for it to finish
  // (this covers loaders, rendering and the play function).
  const phase = await page
    .waitForFunction(
      () => {
        const render = window.__STORYBOOK_PREVIEW__?.currentRender;
        const p = render?.phase;
        return p === 'completed' || p === 'finished' || p === 'errored' || p === 'aborted' ? p : false;
      },
      null,
      { timeout: 30000, polling: 100 }
    )
    .then((h) => h.jsonValue())
    .catch(() => 'timeout');

  await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
  await page.evaluate(() => document.fonts.ready);
  // Recharts animates lines in over ~1.5s; let every chart land before measuring.
  await page.waitForTimeout(1800);
  return { page, context, phase, errors };
}

#!/usr/bin/env node
/**
 * Captures the Phase 4 "after" screenshots from the built Storybook at a 390 px phone width.
 *
 *   cd src/A2S.Web && npm run build-storybook -- --quiet
 *   node ../../scripts/capture-audit-screens.mjs [--static src/A2S.Web/storybook-static] [--out docs/v2/screens-after]
 *
 * Serves the static build locally (Storybook's iframe needs http, not file://), opens each story's
 * iframe at 390×844 with a 2× device scale factor, waits for the network to settle and saves a
 * full-page PNG. Uses the Playwright already in src/A2S.Web; no extra dependency.
 */

import { createServer } from 'node:http';
import { createReadStream, existsSync, mkdirSync, statSync } from 'node:fs';
import { extname, join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const here = fileURLToPath(new URL('.', import.meta.url));
const repoRoot = resolve(here, '..');
const webRoot = join(repoRoot, 'src', 'A2S.Web');

const args = process.argv.slice(2);
const option = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 && args[i + 1] ? args[i + 1] : fallback;
};
const staticDir = resolve(repoRoot, option('--static', join('src', 'A2S.Web', 'storybook-static')));
const outDir = resolve(repoRoot, option('--out', join('docs', 'v2', 'screens-after')));

/** Story ids as Storybook derives them from `title` + export name. */
export const STORIES = [
  // Today
  ['today--default', 'features-today-todaypage--default'],
  ['today--done', 'features-today-todaypage--done'],
  ['today--rest-day', 'features-today-todaypage--rest-day'],
  ['today--cardio-day', 'features-today-todaypage--cardio-day'],
  ['today--paused', 'features-today-todaypage--paused'],
  ['today--no-plan', 'features-today-todaypage--no-plan'],
  ['today--empty', 'features-today-todaypage--empty'],
  ['today--offline-pending', 'features-today-todaypage--offline-pending'],
  ['today--loading', 'features-today-todaypage--loading'],
  ['today--error', 'features-today-todaypage--error'],
  // Habits
  ['habits--default', 'features-habits-habitspage--default'],
  ['habits--empty', 'features-habits-habitspage--empty'],
  ['habits--editing', 'features-habits-habitspage--editing'],
  ['habits--loading', 'features-habits-habitspage--loading'],
  ['habits--error', 'features-habits-habitspage--error'],
  // Progress
  ['progress--on-pace', 'features-progress-progresspage--on-pace'],
  ['progress--behind', 'features-progress-progresspage--behind'],
  ['progress--ahead', 'features-progress-progresspage--ahead'],
  ['progress--no-target', 'features-progress-progresspage--no-target'],
  ['progress--few-points', 'features-progress-progresspage--few-points'],
  ['progress--empty', 'features-progress-progresspage--empty'],
  ['progress--loading', 'features-progress-progresspage--loading'],
  ['progress--error', 'features-progress-progresspage--error'],
];

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.woff2': 'font/woff2',
  '.woff': 'font/woff',
};

function serve(dir) {
  const server = createServer((req, res) => {
    const url = new URL(req.url ?? '/', 'http://localhost');
    let file = join(dir, decodeURIComponent(url.pathname));
    if (!existsSync(file) || statSync(file).isDirectory()) file = join(file, 'index.html');
    if (!existsSync(file)) {
      res.writeHead(404).end();
      return;
    }
    res.writeHead(200, { 'Content-Type': MIME[extname(file)] ?? 'application/octet-stream' });
    createReadStream(file).pipe(res);
  });
  return new Promise((resolveServer) => {
    server.listen(0, '127.0.0.1', () => resolveServer({ server, port: server.address().port }));
  });
}

async function main() {
  if (!existsSync(join(staticDir, 'iframe.html'))) {
    console.error(`No Storybook build at ${staticDir}. Run "npm run build-storybook -- --quiet" in src/A2S.Web first.`);
    process.exit(1);
  }
  mkdirSync(outDir, { recursive: true });

  const require = createRequire(join(webRoot, 'package.json'));
  const { chromium } = require('playwright');

  const { server, port } = await serve(staticDir);
  const browser = await chromium.launch();
  const context = await browser.newContext({
    viewport: { width: 390, height: 844 },
    deviceScaleFactor: 2,
    colorScheme: 'dark',
    isMobile: true,
    hasTouch: true,
  });
  // Nothing may leave the machine: the placeholder Clerk key would otherwise keep retrying
  // clerk-js from the network and the page would never settle.
  await context.route('**/*', (route) => {
    const host = new URL(route.request().url()).hostname;
    return host === '127.0.0.1' || host === 'localhost' ? route.continue() : route.abort();
  });
  const page = await context.newPage();
  page.setDefaultTimeout(15_000);
  const failures = [];

  for (const [name, id] of STORIES) {
    const url = `http://127.0.0.1:${port}/iframe.html?id=${id}&viewMode=story`;
    try {
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      await page.waitForSelector('#storybook-root > *', { timeout: 15_000 });
      await page.waitForTimeout(1_200); // let the stateful mocks' latency and transitions settle
      const target = join(outDir, `${name}.png`);
      await page.screenshot({ path: target, fullPage: true });
      console.log(`saved ${target}`);
    } catch (error) {
      failures.push([name, error instanceof Error ? error.message : String(error)]);
      console.error(`failed ${name}: ${error instanceof Error ? error.message : error}`);
    }
  }

  await browser.close();
  server.close();
  if (failures.length > 0) {
    console.error(`${failures.length} capture(s) failed`);
    process.exit(1);
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});

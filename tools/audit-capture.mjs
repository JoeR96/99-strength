// Re-screenshot audited routes at desktop + mobile for Phase 2 verification.
//
// Precondition: the full stack is running —
//   docker start a2s-audit-pg
//   dotnet run --project src/A2S.Api --launch-profile http
//   (cd src/A2S.Web && npm run dev)
//
// Usage (from repo root):
//   node tools/audit-capture.mjs                       # capture the default route set
//   node tools/audit-capture.mjs /dashboard /workout   # capture only these routes
//
// Screenshots land in audit-screenshots/ as <slug>--1440.png / <slug>--390.png.
// A logged-in Clerk session is persisted to tools/.auth-state.json and reused.

import { readFileSync, existsSync, mkdirSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..');
const authStatePath = resolve(__dirname, '.auth-state.json');
const shotsDir = resolve(repoRoot, 'audit-screenshots');
const FRONTEND = 'http://localhost:5173';

// Read TEST_USER_EMAIL / TEST_USER_PASSWORD from src/A2S.Web/.env.test
function loadEnvTest() {
  const envPath = resolve(repoRoot, 'src/A2S.Web/.env.test');
  const raw = readFileSync(envPath, 'utf8');
  const env = {};
  for (const line of raw.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eq = trimmed.indexOf('=');
    if (eq === -1) continue;
    const key = trimmed.slice(0, eq).trim();
    let val = trimmed.slice(eq + 1).trim();
    if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
      val = val.slice(1, -1);
    }
    env[key] = val;
  }
  return env;
}

// Default route set: every audited screen reachable without a completed session.
const DEFAULT_ROUTES = [
  ['dashboard', '/dashboard'],
  ['workout', '/workout'],
  ['programs', '/programs'],
  ['exercises', '/exercises'],
  ['hevy', '/hevy'],
  ['hevy-data', '/hevy/data'],
  ['settings', '/settings'],
  ['history', '/history'],
  ['simulate', '/simulate'],
  ['setup', '/setup'],
];

function slugFor(route) {
  const clean = route.replace(/^\//, '').replace(/\//g, '-');
  return clean || 'root';
}

async function ensureAuthState(browser, env) {
  if (existsSync(authStatePath)) return;
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto(`${FRONTEND}/sign-in`, { waitUntil: 'networkidle' });
  await page.fill('input[name="identifier"]', env.TEST_USER_EMAIL);
  await page.getByRole('button', { name: /continue/i }).first().click();
  await page.fill('input[name="password"]', env.TEST_USER_PASSWORD);
  await page.getByRole('button', { name: /continue/i }).first().click();
  await page.waitForURL('**/dashboard', { timeout: 30000 });
  await context.storageState({ path: authStatePath });
  await context.close();
}

async function capture(browser, routes) {
  for (const width of [1440, 390]) {
    const height = width === 1440 ? 900 : 844;
    const context = await browser.newContext({
      storageState: authStatePath,
      viewport: { width, height },
    });
    const page = await context.newPage();
    for (const [slug, route] of routes) {
      await page.goto(`${FRONTEND}${route}`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(600);
      await page.screenshot({
        path: resolve(shotsDir, `${slug}--${width}.png`),
        fullPage: true,
      });
      console.log(`captured ${slug}--${width}.png`);
    }
    await context.close();
  }
}

async function main() {
  if (!existsSync(shotsDir)) mkdirSync(shotsDir, { recursive: true });
  const env = loadEnvTest();
  if (!env.TEST_USER_EMAIL || !env.TEST_USER_PASSWORD) {
    throw new Error('TEST_USER_EMAIL / TEST_USER_PASSWORD missing from src/A2S.Web/.env.test');
  }

  // Dynamic import of chromium from playwright
  const playwrightPath = pathToFileURL(resolve(repoRoot, 'src/A2S.Web/node_modules/playwright/index.js')).href;
  const playwrightModule = await import(playwrightPath);
  const chromium = playwrightModule.default.chromium;

  const argRoutes = process.argv.slice(2);
  const routes = argRoutes.length
    ? argRoutes.map((r) => [slugFor(r), r])
    : DEFAULT_ROUTES;

  const browser = await chromium.launch();
  try {
    await ensureAuthState(browser, env);
    await capture(browser, routes);
  } finally {
    await browser.close();
  }
  console.log('done');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});

// Accessibility audit for A2S.Web — axe-core via Playwright.
//
// Precondition: full stack running (frontend :5173, API :5123).
// Auth: reuses a Clerk storageState (path passed via AUTH_STATE env or default).
//
// Usage (from repo root):
//   node tools/a11y/audit.mjs                # full sweep -> tools/a11y/results.json
//   node tools/a11y/audit.mjs --pages=/dashboard,/workout   # subset (static only)
//
// Outputs:
//   tools/a11y/results.json   — machine-readable per-surface violations
//   audit-screenshots/*.png   — post-completion + modal state screenshots
//
// axe config: WCAG 2.0/2.1 A + AA tags. Desktop 1440; contrast pages re-run at 390.

import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const webRoot = resolve(repoRoot, 'src/A2S.Web');
const axePath = resolve(webRoot, 'node_modules/axe-core/axe.min.js');
const shotsDir = resolve(repoRoot, 'audit-screenshots');
const resultsPath = resolve(__dirname, process.env.OUT || 'results.json');
const FRONTEND = 'http://localhost:5173';

const AUTH_STATE =
  process.env.AUTH_STATE ||
  'C:/Users/ADMINI~1/AppData/Local/Temp/claude/c--Users-Administrator-99-strength/7e56d9cb-fd00-497b-a0b7-4fcab3455be6/scratchpad/auth-state.json';

const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

// Logged-in routes to sweep at 1440.
const AUTHED_ROUTES = [
  ['dashboard', '/dashboard'],
  ['workout', '/workout'],
  ['session-1', '/workout/session/1'],
  ['setup', '/setup'],
  ['programs', '/programs'],
  ['exercises', '/exercises'],
  ['hevy', '/hevy'],
  ['hevy-data', '/hevy/data'],
  ['settings', '/settings'],
  ['history', '/history'],
  ['simulate', '/simulate'],
];

// Logged-out routes.
const PUBLIC_ROUTES = [
  ['sign-in', '/sign-in'],
  ['sign-up', '/sign-up'],
];

// Contrast-relevant pages to also run at 390 (mobile).
const MOBILE_ROUTES = [
  ['dashboard', '/dashboard'],
  ['workout', '/workout'],
  ['session-1', '/workout/session/1'],
  ['programs', '/programs'],
  ['exercises', '/exercises'],
  ['settings', '/settings'],
  ['history', '/history'],
];

function ensureDir(d) {
  if (!existsSync(d)) mkdirSync(d, { recursive: true });
}

async function runAxe(page) {
  await page.addScriptTag({ path: axePath });
  const result = await page.evaluate(async (tags) => {
    // eslint-disable-next-line no-undef
    const res = await window.axe.run(document, {
      runOnly: { type: 'tag', values: tags },
      resultTypes: ['violations'],
    });
    return res.violations.map((v) => ({
      id: v.id,
      impact: v.impact,
      help: v.help,
      helpUrl: v.helpUrl,
      nodes: v.nodes.map((n) => ({
        target: n.target,
        html: n.html.slice(0, 300),
        failureSummary: n.failureSummary,
        impact: n.impact,
      })),
    }));
  }, AXE_TAGS);
  return result;
}

function summarize(violations) {
  const counts = { critical: 0, serious: 0, moderate: 0, minor: 0 };
  for (const v of violations) {
    for (const n of v.nodes) {
      const imp = n.impact || v.impact || 'minor';
      if (counts[imp] !== undefined) counts[imp] += 1;
    }
  }
  return counts;
}

async function auditSurface(page, name, width, results) {
  const violations = await runAxe(page);
  const counts = summarize(violations);
  results.push({ surface: name, width, counts, violations });
  const total = Object.values(counts).reduce((a, b) => a + b, 0);
  console.log(
    `  ${name} @${width}: ${total} nodes (c:${counts.critical} s:${counts.serious} mo:${counts.moderate} mi:${counts.minor})`
  );
  return violations;
}

async function main() {
  ensureDir(shotsDir);
  const mode = process.argv.find((a) => a.startsWith('--pages='));
  const subsetPages = mode ? mode.split('=')[1].split(',') : null;

  const playwrightPath = pathToFileURL(
    resolve(webRoot, 'node_modules/playwright/index.js')
  ).href;
  const { chromium } = (await import(playwrightPath)).default;

  const results = [];
  const browser = await chromium.launch();

  try {
    // ---- Logged-in static routes @1440 ----
    const ctx1440 = await browser.newContext({
      storageState: AUTH_STATE,
      viewport: { width: 1440, height: 900 },
    });
    const page = await ctx1440.newPage();
    page.on('pageerror', () => {});

    const authedToRun = subsetPages
      ? AUTHED_ROUTES.filter(([, r]) => subsetPages.includes(r))
      : AUTHED_ROUTES;

    console.log(`== Authed routes @1440 (${authedToRun.length}) ==`);
    for (const [slug, route] of authedToRun) {
      try {
        await page.goto(`${FRONTEND}${route}`, { waitUntil: 'networkidle' });
        await page.waitForTimeout(800);
        await auditSurface(page, `route${route}`, 1440, results);
      } catch (e) {
        console.log(`  route${route} @1440 FAILED: ${e.message}`);
      }
    }

    // ---- Stateful modal surfaces (only in full mode) ----
    if (!subsetPages) {
      console.log('== Stateful modals @1440 ==');

      // Progression modal: click an exercise card on /workout
      await page.goto(`${FRONTEND}/workout`, { waitUntil: 'networkidle' }).catch(() => {});
      await page.waitForTimeout(800);
      try {
        const card = page.locator('div.cursor-pointer').filter({ hasText: /Squat|Bench|Press|Curl|Row|Pull/ }).first();
        await card.click({ timeout: 5000 });
        await page.waitForTimeout(600);
        await auditSurface(page, 'modal:progression', 1440, results);
        await page.screenshot({ path: resolve(shotsDir, 'a11y-progression-modal--1440.png'), fullPage: true });
        await page.keyboard.press('Escape');
        await page.waitForTimeout(300);
      } catch (e) {
        console.log('  progression modal: could not open —', e.message);
      }

      // Edit-exercises modal: DayCard edit (pencil) button
      await page.goto(`${FRONTEND}/workout`, { waitUntil: 'networkidle' }).catch(() => {});
      await page.waitForTimeout(800);
      try {
        const editBtn = page.locator('button[title="Edit exercises"]').first();
        await editBtn.click({ timeout: 5000 });
        await page.waitForTimeout(600);
        await auditSurface(page, 'modal:edit-exercises', 1440, results);
        await page.screenshot({ path: resolve(shotsDir, 'a11y-edit-exercises-modal--1440.png'), fullPage: true });
        await page.keyboard.press('Escape');
        await page.waitForTimeout(300);
      } catch (e) {
        console.log('  edit-exercises modal: could not open —', e.message);
      }

      // Substitution modal: swap icon on session page
      await page.goto(`${FRONTEND}/workout/session/1`, { waitUntil: 'networkidle' }).catch(() => {});
      await page.waitForTimeout(1000);
      try {
        const swapBtn = page.locator('button[aria-label="Substitute exercise"]').first();
        await swapBtn.click({ timeout: 5000 });
        await page.waitForTimeout(700);
        await auditSurface(page, 'modal:substitution', 1440, results);
        await page.screenshot({ path: resolve(shotsDir, 'a11y-substitution-modal--1440.png'), fullPage: true });
        await page.keyboard.press('Escape');
        await page.waitForTimeout(300);
      } catch (e) {
        console.log('  substitution modal: could not open —', e.message);
      }
    }

    await ctx1440.close();

    // ---- Mobile @390 contrast pages ----
    const ctx390 = await browser.newContext({
      storageState: AUTH_STATE,
      viewport: { width: 390, height: 844 },
    });
    const mpage = await ctx390.newPage();
    mpage.on('pageerror', () => {});
    const mobileToRun = subsetPages
      ? MOBILE_ROUTES.filter(([, r]) => subsetPages.includes(r))
      : MOBILE_ROUTES;
    console.log('== Authed routes @390 ==');
    for (const [slug, route] of mobileToRun) {
      await mpage.goto(`${FRONTEND}${route}`, { waitUntil: 'networkidle' }).catch(() => {});
      await mpage.waitForTimeout(700);
      await auditSurface(mpage, `route${route}`, 390, results);
    }
    await ctx390.close();

    // ---- Logged-out public routes ----
    if (!subsetPages) {
      const ctxPub = await browser.newContext({ viewport: { width: 1440, height: 900 } });
      const ppage = await ctxPub.newPage();
      ppage.on('pageerror', () => {});
      console.log('== Public routes @1440 ==');
      for (const [slug, route] of PUBLIC_ROUTES) {
        await ppage.goto(`${FRONTEND}${route}`, { waitUntil: 'networkidle' }).catch(() => {});
        await ppage.waitForTimeout(1200);
        await auditSurface(ppage, `route${route}`, 1440, results);
      }
      await ctxPub.close();
    }
  } finally {
    await browser.close();
  }

  writeFileSync(resultsPath, JSON.stringify(results, null, 2));
  console.log(`\nWrote ${resultsPath}`);

  // Print rule-grouped summary
  const byRule = {};
  for (const r of results) {
    for (const v of r.violations) {
      byRule[v.id] = byRule[v.id] || { impact: v.impact, surfaces: new Set(), nodes: 0 };
      byRule[v.id].surfaces.add(`${r.surface}@${r.width}`);
      byRule[v.id].nodes += v.nodes.length;
    }
  }
  console.log('\n== Violations by rule ==');
  for (const [id, info] of Object.entries(byRule).sort((a, b) => b[1].nodes - a[1].nodes)) {
    console.log(`  ${id} [${info.impact}] — ${info.nodes} nodes across ${info.surfaces.size} surfaces`);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});

// Drive the post-completion flow: log all sets on /workout/session/1, complete the
// workout, audit the CompletionSummary + any weight-confirmation modal, screenshot
// each, then audit /workout so the COMPLETED DayCard (bg-success/10) is present.
//
// Mutates the seeded dev account (intended). Appends results to results-post.json.
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const repoRoot = 'C:/Users/Administrator/99-strength';
const webRoot = resolve(repoRoot, 'src/A2S.Web');
const axePath = resolve(webRoot, 'node_modules/axe-core/axe.min.js');
const shotsDir = resolve(repoRoot, 'audit-screenshots');
const AUTH = 'C:/Users/ADMINI~1/AppData/Local/Temp/claude/c--Users-Administrator-99-strength/7e56d9cb-fd00-497b-a0b7-4fcab3455be6/scratchpad/auth-state.json';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

if (!existsSync(shotsDir)) mkdirSync(shotsDir, { recursive: true });
const { chromium } = (await import(pathToFileURL(resolve(webRoot, 'node_modules/playwright/index.js')).href)).default;

async function runAxe(page) {
  await page.addScriptTag({ path: axePath });
  return page.evaluate(async (tags) => {
    const res = await window.axe.run(document, { runOnly: { type: 'tag', values: tags }, resultTypes: ['violations'] });
    return res.violations.map((v) => ({
      id: v.id, impact: v.impact,
      nodes: v.nodes.map((n) => ({ target: n.target, html: n.html.slice(0, 260), failureSummary: n.failureSummary, impact: n.impact })),
    }));
  }, AXE_TAGS);
}
function summarize(violations) {
  const c = { critical: 0, serious: 0, moderate: 0, minor: 0 };
  for (const v of violations) for (const n of v.nodes) { const i = n.impact || v.impact || 'minor'; if (c[i] !== undefined) c[i]++; }
  return c;
}

const results = [];
const b = await chromium.launch();
const ctx = await b.newContext({ storageState: AUTH, viewport: { width: 1440, height: 900 } });
const page = await ctx.newPage();
page.on('dialog', (d) => d.accept().catch(() => {}));

try {
  await page.goto('http://localhost:5173/workout/session/1', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1500);

  // If cards are collapsed (Hevy prefill), expand them all first.
  const expandables = page.locator('[role="button"][aria-label^="Expand"]');
  const nExp = await expandables.count();
  for (let i = 0; i < nExp; i++) {
    try { await expandables.nth(0).click(); await page.waitForTimeout(150); } catch { /* re-query */ }
  }
  await page.waitForTimeout(400);

  // Click every "Log" / "Log AMRAP" button until none remain enabled.
  for (let pass = 0; pass < 60; pass++) {
    const logBtns = page.locator('button', { hasText: /^(Log|Log AMRAP)$/ });
    const n = await logBtns.count();
    let clicked = 0;
    for (let i = 0; i < n; i++) {
      const btn = logBtns.nth(i);
      try {
        if (await btn.isVisible() && await btn.isEnabled()) { await btn.click({ timeout: 1500 }); clicked++; await page.waitForTimeout(60); }
      } catch { /* ignore */ }
    }
    if (clicked === 0) break;
  }
  await page.waitForTimeout(500);

  const completeBtn = page.locator('[data-testid="complete-workout-button"]');
  const enabled = await completeBtn.isEnabled().catch(() => false);
  console.log('complete button enabled:', enabled);
  await page.screenshot({ path: resolve(shotsDir, 'a11y-session-all-logged--1440.png'), fullPage: true });

  if (enabled) {
    await completeBtn.click();
    await page.waitForTimeout(1200);

    // A weight-confirmation modal may appear before the summary.
    const wcHeading = page.locator('text=/confirm.*weight|working weight/i').first();
    if (await wcHeading.isVisible().catch(() => false)) {
      const v = await runAxe(page);
      results.push({ surface: 'modal:weight-confirmation', width: 1440, counts: summarize(v), violations: v });
      await page.screenshot({ path: resolve(shotsDir, 'a11y-weight-confirmation-modal--1440.png'), fullPage: true });
      console.log('weight-confirmation modal: audited');
      // dismiss (Skip/Confirm) to reach summary
      const skip = page.locator('button', { hasText: /skip|confirm|continue/i }).first();
      if (await skip.isVisible().catch(() => false)) { await skip.click().catch(() => {}); await page.waitForTimeout(1000); }
    }

    // CompletionSummary
    const summaryVisible = await page.locator('[data-testid="completion-title"]').isVisible().catch(() => false);
    console.log('completion summary visible:', summaryVisible);
    if (summaryVisible) {
      const v = await runAxe(page);
      results.push({ surface: 'completion-summary', width: 1440, counts: summarize(v), violations: v });
      await page.screenshot({ path: resolve(shotsDir, 'a11y-completion-summary--1440.png'), fullPage: true });
      // Scroll to the "New Weights Next Session" card if present and shot it framed
      const nw = page.locator('[data-testid="new-weights-card"]');
      if (await nw.count()) {
        await nw.scrollIntoViewIfNeeded();
        await page.waitForTimeout(300);
        await nw.screenshot({ path: resolve(shotsDir, 'a11y-new-weights-card--1440.png') }).catch(() => {});
        console.log('new-weights-card: present + shot');
      } else {
        console.log('new-weights-card: NOT present this session');
      }
    }
  }

  // Now audit /workout to capture the COMPLETED DayCard state
  await page.goto('http://localhost:5173/workout', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1200);
  const vW = await runAxe(page);
  results.push({ surface: 'workout-after-completion', width: 1440, counts: summarize(vW), violations: vW });
  await page.screenshot({ path: resolve(shotsDir, 'a11y-workout-completed-daycard--1440.png'), fullPage: true });
  const hasCompleted = await page.locator('[data-testid$="-completed-icon"]').count();
  console.log('completed day-card icons on /workout:', hasCompleted);
} finally {
  writeFileSync(resolve('C:/Users/Administrator/99-strength/tools/a11y', 'results-post.json'), JSON.stringify(results, null, 2));
  await b.close();
}
console.log('post-completion done');

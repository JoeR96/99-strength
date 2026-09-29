/**
 * `npm run showcase` — screenshot a curated set of Storybook stories for the
 * portfolio (enjoeneer.dev). Expects a fresh `storybook-static/` (the npm script
 * builds it first), serves it, captures each story with Playwright (Chromium) and
 * writes WebP files plus a README to `showcase/`.
 *
 *   node scripts/showcase.mjs            # everything
 *   node scripts/showcase.mjs dashboard  # only shots whose file name matches
 */
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';
import { serveStatic, launchBrowser, openStory } from './storybook-harness.mjs';

const OUT = 'showcase';
const MAX_BYTES = 250 * 1024;
const DESKTOP = { width: 1440, height: 900, deviceScaleFactor: 1 };
const PHONE = { width: 390, height: 844, deviceScaleFactor: 2 };

/**
 * file: output name (no extension) · story: Storybook id · caption: README text
 * scrollTo: optional text to scroll to the top of the viewport before capture
 */
const SHOTS = [
  { file: 'dashboard', story: 'pages-dashboard--mid-program', caption: 'Dashboard in week 11: quick stats, the current program and this week\'s four sessions with Hevy sync state.' },
  { file: 'program-overview', story: 'pages-workout--mid-program', caption: 'Program overview: block sequence, overall progress and the week\'s training days.' },
  { file: 'session-logging', story: 'pages-workout-session--logging-sets', caption: 'Logging bench day: three working sets done, the AMRAP set up next with its rep-out target.' },
  { file: 'session-complete', story: 'features-workout-completion-summary--bench-day-complete', caption: 'After "Complete Workout": what progressed and next week\'s plan for the same day.' },
  { file: 'setup-template', story: 'pages-setup-wizard--choose-template', caption: 'Setup wizard, template step: pick a pre-built A2S program.' },
  { file: 'setup-exercises', story: 'pages-setup-wizard--exercises', scrollTo: 'Your Program', caption: 'Setup wizard, exercise step: the template\'s lifts laid out by day, ready to edit.' },
  { file: 'programs', story: 'pages-programs--three-programs', caption: 'Programs: the active program, a finished block and a draft.' },
  { file: 'exercise-library', story: 'pages-exercise-library--grouped', caption: 'Exercise library: the Hevy catalogue grouped by muscle, with muscle and equipment filters.' },
  { file: 'exercise-history', story: 'pages-exercise-library--squat-history', caption: 'Squat history from Hevy: sessions, PR weight, best volume and max weight over eight months.' },
  { file: 'e1rm-chart', story: 'features-charts-exercise-history-chart--squat-estimated-one-rep-max', caption: 'Estimated 1RM (Epley) for the squat across two programs, with time-range and metric filters.' },
  { file: 'history-calendar', story: 'pages-history--session-detail', caption: 'History: training days coloured by block, with one session\'s sets and AMRAP results.' },
  { file: 'simulator', story: 'pages-simulator--projection', caption: 'Simulator: training-max projection to the end of the program using the real progression rules.', scrollTo: 'Training Max Progression' },
  { file: 'hevy-sync', story: 'pages-hevy--connected', caption: 'Hevy integration: connected account and the routines synced into the program\'s folder.' },
  { file: 'hevy-workouts', story: 'pages-hevy-data--synced-workouts', caption: 'Workouts pulled back from Hevy, newest first, with best set and volume per exercise.' },
  { file: 'settings', story: 'pages-settings--default', caption: 'Settings: seed test data and export the program as JSON.' },
  { file: 'sign-in', story: 'pages-sign-in--default', caption: 'Sign-in: Clerk\'s widget themed to the app, every text pair at WCAG AA.' },
  // Phone
  { file: 'phone-dashboard', story: 'pages-dashboard--mid-program', caption: 'Dashboard on a phone.', viewport: PHONE },
  { file: 'phone-session', story: 'pages-workout-session--logging-sets', caption: 'Set logging on a phone.', viewport: PHONE },
  { file: 'phone-history', story: 'pages-history--calendar', caption: 'History calendar on a phone.', viewport: PHONE, scrollTo: 'Activity Calendar' },
  { file: 'phone-sign-in', story: 'pages-sign-in--default', caption: 'Sign-in on a phone.', viewport: PHONE },
];

async function toWebp(png) {
  for (const quality of [90, 84, 78, 70, 62, 54]) {
    const buf = await sharp(png).webp({ quality, effort: 6 }).toBuffer();
    if (buf.length <= MAX_BYTES) return { buf, quality };
  }
  const buf = await sharp(png).webp({ quality: 45, effort: 6 }).toBuffer();
  return { buf, quality: 45 };
}

async function scrollToText(page, text) {
  await page.evaluate((t) => {
    const el = [...document.querySelectorAll('h1,h2,h3,button,[class*="CardTitle"],div')].find(
      (n) => n.childElementCount === 0 && (n.textContent?.trim() ?? '').startsWith(t)
    );
    if (!el) return;
    el.scrollIntoView({ block: 'start' });
    // Clear the sticky navbar (h-16) and give the heading some air.
    window.scrollBy(0, -88);
  }, text);
  await page.waitForTimeout(400);
}

const filter = process.argv[2] ? new RegExp(process.argv[2]) : null;
fs.mkdirSync(OUT, { recursive: true });
const server = await serveStatic('storybook-static');
const browser = await launchBrowser();
const results = [];
try {
  for (const shot of SHOTS) {
    if (filter && !filter.test(shot.file)) continue;
    const viewport = shot.viewport ?? DESKTOP;
    const { page, context, phase, errors } = await openStory(browser, server.url, shot.story, viewport);
    if (phase !== 'finished' && phase !== 'completed') console.warn(`! ${shot.story}: render phase ${phase}`);
    if (errors.length) console.warn(`! ${shot.story}: ${errors[0]}`);
    // Play functions can leave the page scrolled; start every shot from the top.
    await page.evaluate(() => window.scrollTo(0, 0));
    if (shot.scrollTo) await scrollToText(page, shot.scrollTo);
    const png = await page.screenshot({ type: 'png' });
    const { buf, quality } = await toWebp(png);
    const file = path.join(OUT, `${shot.file}.webp`);
    fs.writeFileSync(file, buf);
    const kb = Math.round(buf.length / 1024);
    results.push({ ...shot, viewport, kb });
    console.log(`${file}  ${viewport.width}×${viewport.height}@${viewport.deviceScaleFactor}x  q${quality}  ${kb} KB`);
    await context.close();
  }
} finally {
  await browser.close();
  await server.close();
}

if (!filter) {
  const rows = results
    .map((r) => {
      const size = `${r.viewport.width * r.viewport.deviceScaleFactor}×${r.viewport.height * r.viewport.deviceScaleFactor}`;
      return `| \`${r.file}.webp\` | ${size} | ${r.caption} | \`${r.story}\` |`;
    })
    .join('\n');
  fs.writeFileSync(
    path.join(OUT, 'README.md'),
    `# 99 Strength showcase screenshots

Generated by \`npm run showcase\` (\`scripts/showcase.mjs\`): Storybook is built, served locally and each
story below is captured with Playwright (Chromium) against the MSW mock data in \`src/mocks\`
(a lifter in week 11 of a 21-week A2S program). Desktop shots are 1440×900 at 1x; phone shots are
390×844 at 2x. WebP, each under 250 KB. Re-run after UI changes; the fixtures are seeded, but dates
are relative to the day you run it.

| File | Pixels | What it shows | Story |
|---|---|---|---|
${rows}
`
  );
  console.log(`wrote ${OUT}/README.md`);
}

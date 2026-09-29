/**
 * `npm run a11y:stories` — run axe-core (WCAG 2.0/2.1 A + AA) over every story in the
 * static Storybook build at 1440 wide (and 390 wide for page stories), after each
 * story's play function has finished. Writes a JSON report and prints a summary.
 *
 *   node scripts/a11y-stories.mjs [outFile] [storyIdRegex]
 * Exit code 1 when any violation is found.
 */
import fs from 'node:fs';
import { AxeBuilder } from '@axe-core/playwright';
import { serveStatic, listStories, launchBrowser, openStory } from './storybook-harness.mjs';

const OUT = process.argv[2] ?? 'a11y-results.json';
const filter = process.argv[3] ? new RegExp(process.argv[3]) : null;
const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

const server = await serveStatic('storybook-static');
const browser = await launchBrowser();
const report = [];
try {
  for (const story of listStories('storybook-static')) {
    if (filter && !filter.test(story.id)) continue;
    const widths = story.id.startsWith('pages-') ? [1440, 390] : [1440];
    for (const width of widths) {
      const { page, context, phase } = await openStory(browser, server.url, story.id, {
        width,
        height: width < 600 ? 844 : 900,
      });
      const results = await new AxeBuilder({ page })
        .withTags(TAGS)
        .analyze();
      const violations = results.violations.map((v) => ({
        id: v.id,
        impact: v.impact,
        help: v.help,
        nodes: v.nodes.map((n) => ({ target: n.target.join(' '), summary: n.failureSummary?.split('\n').slice(0, 3).join(' ') })),
      }));
      const count = violations.reduce((a, v) => a + v.nodes.length, 0);
      report.push({ story: story.id, width, phase, count, violations });
      console.log(`${count ? '✗' : '✓'} ${story.id} @${width}${phase !== 'finished' ? ` (${phase})` : ''}${count ? `  ${count} node(s): ${violations.map((v) => `${v.id}×${v.nodes.length}`).join(', ')}` : ''}`);
      await context.close();
    }
  }
} finally {
  await browser.close();
  await server.close();
}

fs.writeFileSync(OUT, JSON.stringify(report, null, 2));
const byRule = {};
for (const r of report) for (const v of r.violations) byRule[v.id] = (byRule[v.id] ?? 0) + v.nodes.length;
const total = Object.values(byRule).reduce((a, b) => a + b, 0);
console.log(`\n${report.length} story renders, ${total} violating node(s)`, byRule);
process.exit(total ? 1 : 0);

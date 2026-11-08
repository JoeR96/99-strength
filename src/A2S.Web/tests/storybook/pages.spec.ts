import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { listPageStories, storyUrl } from '../../scripts/storybook-harness.mjs';
import { knownIssue, type Check } from './known-issues';

// Brief § 6: Playwright on a mobile viewport (390 × 844) for the main journeys. Every page-level
// story is opened through the built Storybook's iframe and must: fit the phone width with no
// horizontal overflow, give every button and link a 44 px touch target, and pass axe with no
// serious or critical violations. The list of stories comes from storybook-static/index.json,
// so a new page story is covered the moment it is built.

const MIN_TAP_PX = 44;
// Buttons and links hidden from the person (display:none, zero size, or off-stage) are skipped;
// so are elements inside Radix portals that are closed. Storybook's own chrome is not in the iframe.
const TAP_TARGETS = 'a[href], button, [role="button"], input[type="button"], input[type="submit"]';

const stories = listPageStories();

test.describe('page stories on a 390 px phone', () => {
  test('there are page stories to check', () => {
    expect(stories.length, 'storybook-static/index.json has no page-level stories').toBeGreaterThan(0);
  });

  for (const story of stories) {
    test.describe(story.id, () => {
      test.beforeEach(async ({ page, baseURL }) => {
        // Page stories wrap ClerkProvider with a placeholder key on clerk.example.com; keep the run offline.
        await page.route(/^https?:\/\/(?!127\.0\.0\.1|localhost)/, (route) => route.abort());
        await page.goto(storyUrl(baseURL!, story.id));
        await page.locator('#storybook-root').waitFor({ state: 'attached' });
        // Storybook marks the root once the story has rendered (or errored).
        await page.waitForFunction(() => {
          const root = document.querySelector('#storybook-root');
          return root !== null && (root.childElementCount > 0 || document.querySelector('.sb-show-errordisplay') !== null);
        });
        await expect(page.locator('.sb-show-errordisplay'), 'the story threw while rendering').toHaveCount(0);
        await page.evaluate(() => document.fonts?.ready);
        // Stories resolve their mocked API calls asynchronously; measure only once the DOM has
        // stopped changing for half a second (a "Loading" story settles on its skeleton).
        await page.waitForLoadState('networkidle').catch(() => undefined);
        await page.waitForFunction(
          () =>
            new Promise<boolean>((resolve) => {
              const before = document.body.innerHTML.length;
              setTimeout(() => resolve(document.body.innerHTML.length === before), 500);
            }),
          undefined,
          { timeout: 15_000, polling: 100 },
        );
      });

      // A defect listed in known-issues.ts is an expected failure: the run stays green, and the
      // entry has to be deleted once the page is fixed, or Playwright reports "passed unexpectedly".
      // Set STORYBOOK_NO_BASELINE=1 to see every defect fail for real.
      const expectKnown = (check: Check) => {
        const issue = process.env.STORYBOOK_NO_BASELINE ? undefined : knownIssue(story.id, check);
        test.fail(issue !== undefined, issue ? `known issue: ${issue.reason}` : '');
      };

      test('no horizontal overflow', async ({ page }) => {
        expectKnown('overflow');
        const overflow = await page.evaluate(() => {
          const doc = document.documentElement;
          const widest = Math.max(doc.scrollWidth, document.body.scrollWidth);
          const offenders = [...document.body.querySelectorAll<HTMLElement>('*')]
            .filter((el) => {
              const rect = el.getBoundingClientRect();
              return rect.width > 0 && rect.right > window.innerWidth + 1 && getComputedStyle(el).position !== 'fixed';
            })
            .slice(0, 5)
            .map((el) => `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''}.${[...el.classList].slice(0, 3).join('.')} right=${Math.round(el.getBoundingClientRect().right)}`);
          return { viewport: window.innerWidth, widest, offenders };
        });

        expect(overflow.widest, `page scrolls sideways: ${overflow.offenders.join('; ')}`).toBeLessThanOrEqual(overflow.viewport);
      });

      test('buttons and links are at least 44 px', async ({ page }) => {
        expectKnown('tap-targets');
        const small = await findSmallTapTargets(page);
        expect(small, `tap targets under ${MIN_TAP_PX} px:\n${small.join('\n')}`).toEqual([]);
      });

      test('axe finds no serious or critical violations', async ({ page }) => {
        expectKnown('axe');
        const results = await runAxe(page);

        const blocking = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
        const summary = blocking.map((v) => `${v.id} (${v.impact}): ${v.help}\n  ${v.nodes.slice(0, 3).map((n) => n.target.join(' ')).join('\n  ')}`);
        expect(blocking, summary.join('\n')).toEqual([]);
      });
    });
  }
});

/**
 * @storybook/addon-a11y runs its own axe pass inside the iframe and axe refuses to start while
 * another run is in flight, so wait for it to go idle and retry on the race.
 */
async function runAxe(page: Page) {
  const builder = () =>
    new AxeBuilder({ page })
      .include('#storybook-root')
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'best-practice']);

  for (let attempt = 1; ; attempt++) {
    await page.waitForFunction(() => {
      const axe = (window as unknown as { axe?: { _running?: boolean } }).axe;
      return !axe || !axe._running;
    });
    try {
      return await builder().analyze();
    } catch (error) {
      if (attempt >= 5 || !String(error).includes('already running')) throw error;
      await page.waitForTimeout(300 * attempt);
    }
  }
}

async function findSmallTapTargets(page: Page): Promise<string[]> {
  return page.evaluate(
    ({ selector, min }) => {
      const isVisible = (el: HTMLElement) => {
        const style = getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
        if (el.closest('[aria-hidden="true"]') !== null) return false;
        const rect = el.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
      };

      return [...document.querySelectorAll<HTMLElement>(selector)]
        .filter(isVisible)
        .map((el) => {
          // A visually small control still passes when its hit area (padding or a parent label) reaches 44 px.
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
    },
    { selector: TAP_TARGETS, min: MIN_TAP_PX },
  );
}

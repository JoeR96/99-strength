import { test, expect } from '@playwright/test';
import { storyUrl } from '../../scripts/storybook-harness.mjs';

// Brief § 4 offline-first habits, Phase 4 PR 7 task 7.3: the Today/Offline story stubs the API in
// memory and gives the page a "Go offline / Go online" switch that flips navigator.onLine and fires
// the browser events. With no signal, two water taps must show straight away and be counted on
// the pending badge; when the signal returns they replay in order and the stubbed server catches up.

const STORY_ID = 'features-today-todaypage--offline';
const WATER_ID = 'habit-water-0000-4000-8000-000000000001';

test.describe('offline journey (Today/Offline story)', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await page.route(/^https?:\/\/(?!127\.0\.0\.1|localhost)/, (route) => route.abort());
    await page.goto(storyUrl(baseURL!, STORY_ID));
    await expect(page.getByTestId(`habit-counter-${WATER_ID}`)).toBeVisible();
  });

  test('taps while offline show at once, count on the badge, and replay when the connection returns', async ({ page }) => {
    const water = page.getByTestId(`habit-counter-${WATER_ID}`);
    const value = water.locator('[aria-live="polite"]');
    const plus = water.getByRole('button', { name: /add one water/i });
    const serverWater = page.getByTestId('mock-server-water');

    await expect(value).toHaveText('0 / 6');
    await expect(serverWater).toHaveText('0');
    await expect(page.getByTestId('pending-sync')).toHaveCount(0);

    await page.getByRole('button', { name: /go offline/i }).click();
    await plus.click();
    await plus.click();

    await expect(value).toHaveText('2 / 6');
    await expect(page.getByTestId('pending-sync')).toHaveText(/2 pending/);
    await expect(page.getByTestId('pending-sync')).toHaveAttribute('aria-label', '2 changes waiting to sync');
    // Nothing reached the server while offline.
    await page.waitForTimeout(500);
    await expect(serverWater).toHaveText('0');

    await page.getByRole('button', { name: /go online/i }).click();

    await expect(page.getByTestId('pending-sync')).toHaveCount(0);
    await expect(serverWater).toHaveText('2');
    await expect(value).toHaveText('2 / 6');
  });

  test('a queued tap is stored in IndexedDB, not page memory, so a reload or service-worker update keeps it', async ({ page }) => {
    const water = page.getByTestId(`habit-counter-${WATER_ID}`);
    const plus = water.getByRole('button', { name: /add one water/i });

    await page.getByRole('button', { name: /go offline/i }).click();
    await plus.click();
    await expect(page.getByTestId('pending-sync')).toHaveText(/1 pending/);

    // The queue lives in IndexedDB, not in page or service-worker memory. The story opens a fresh
    // database per mount, so prove persistence the way the unit tests do: the op is in the store.
    const queued = await page.evaluate(async () => {
      const names = await indexedDB.databases();
      const story = names.find((d) => d.name?.startsWith('a2s-offline-story-'));
      if (!story?.name) return -1;
      return new Promise<number>((resolve, reject) => {
        const open = indexedDB.open(story.name!);
        open.onerror = () => reject(open.error);
        open.onsuccess = () => {
          const db = open.result;
          const count = db.transaction('ops').objectStore('ops').count();
          count.onsuccess = () => {
            db.close();
            resolve(count.result);
          };
          count.onerror = () => reject(count.error);
        };
      });
    });
    expect(queued).toBe(1);
  });
});

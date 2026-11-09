import { test, expect, settle, shotAndMeasure } from './harness';

// Signed out: every route must land on /sign-in once, with something painted and no loop.
const ROUTES = [
  '/',
  '/today',
  '/dashboard',
  '/workout',
  '/workout/session/1',
  '/habits',
  '/progress',
  '/settings',
  '/meals',
  '/shopping-list',
  '/hevy',
  '/hevy/inbox',
  '/hevy/data',
  '/history',
  '/programs',
  '/exercises',
  '/setup',
  '/simulate',
  '/no-such-page',
];

test.describe('signed out', () => {
  test('sign-in and sign-up pages render', async ({ page }) => {
    await page.goto('/sign-in');
    await expect(page.locator('input[name="identifier"]')).toBeVisible({ timeout: 30_000 });
    await shotAndMeasure(page, 'signed-out--sign-in');

    await page.goto('/sign-up');
    await expect(page.locator('input[name="emailAddress"]')).toBeVisible({ timeout: 30_000 });
    await shotAndMeasure(page, 'signed-out--sign-up');
  });

  for (const route of ROUTES) {
    test(`deep link ${route} lands on sign-in without looping`, async ({ page }) => {
      const navigations: string[] = [];
      const onNav = (frame: import('@playwright/test').Frame) => {
        if (frame === page.mainFrame()) navigations.push(new URL(frame.url()).pathname);
      };
      page.on('framenavigated', onNav);
      await page.goto(route);
      await page.waitForURL(/\/sign-in/, { timeout: 30_000 });
      await expect(page.locator('input[name="identifier"]')).toBeVisible({ timeout: 30_000 });
      await settle(page);
      page.off('framenavigated', onNav);
      expect(navigations.length, `navigations: ${navigations.join(' -> ')}`).toBeLessThanOrEqual(4);
    });
  }
});

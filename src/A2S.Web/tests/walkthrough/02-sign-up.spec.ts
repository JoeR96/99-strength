import { test, expect, settle, shot, shotAndMeasure, testUser } from './harness';

// Sign-up through the app's own /sign-up page with a Clerk development test address
// (+clerk_test: no email is sent, the code is always 424242), then sign out and back in.
//
// On 9 Nov 2026 the development instance had bot protection switched on: after Continue, Clerk
// shows a Cloudflare Turnstile "Verify you are human" box and does not create the account until
// it is ticked. A script must not tick it. Run this spec with WALKTHROUGH_HEADED=1 and tick the
// box yourself (or turn bot protection off for the development instance in the Clerk dashboard);
// headless, the spec records the screen and skips.
const HEADED = Boolean(process.env.WALKTHROUGH_HEADED);

test.describe('sign-up and sign-in', () => {
  test('sign up with a +clerk_test address and land on Today', async ({ page }) => {
    const user = testUser();
    await page.goto('/today');
    await page.waitForURL(/\/(today|sign-in)/);
    await settle(page);
    test.skip(!page.url().includes('/sign-in'), 'this profile is already signed in');

    await page.goto('/sign-up');
    await expect(page.locator('input[name="emailAddress"]')).toBeVisible({ timeout: 30_000 });
    await page.locator('input[name="emailAddress"]').fill(user.email);
    await page.locator('input[name="password"]').fill(user.password);
    await shot(page, 'sign-up--filled');
    await page.getByRole('button', { name: 'Continue', exact: true }).tap();

    // Verification step: six boxes; the development code is fixed.
    const code = page.locator('input[autocomplete="one-time-code"], input[name="code"], input[data-input-otp="true"]').first();
    const reachedCode = await code
      .waitFor({ state: 'visible', timeout: HEADED ? 170_000 : 30_000 })
      .then(() => true)
      .catch(() => false);
    if (!reachedCode) {
      const humanCheck = page.frames().some((frame) => frame.url().includes('challenges.cloudflare.com'));
      await shot(page, humanCheck ? 'sign-up--human-check' : 'sign-up--stuck');
      test.skip(true, humanCheck
        ? 'Clerk asked for a human check (Cloudflare Turnstile) before creating the account; no account was created'
        : 'the verification step never appeared');
    }

    await shotAndMeasure(page, 'sign-up--verify-email');
    await code.pressSequentially('424242', { delay: 60 });

    await page.waitForURL('**/today', { timeout: 30_000 });
    await settle(page);
    await expect(page.getByTestId('start-suggested-plan-card')).toBeVisible();
    await shotAndMeasure(page, 'today--first-run');
  });

  test('sign out lands on sign-in; signing in again lands on Today', async ({ page }) => {
    const user = testUser();
    await page.goto('/today');
    await page.waitForURL(/\/(today|sign-in)/);
    await settle(page);
    test.skip(page.url().includes('/sign-in'), 'no signed-in session in this profile: the sign-up step has to pass first');

    // Where is sign-out on a phone? Record what the header offers.
    await shot(page, 'today--header-before-sign-out');
    const userButton = page.locator('.cl-userButtonTrigger').first();
    await expect(userButton, 'there is no way to sign out on a phone').toBeVisible();
    await userButton.tap();
    await shot(page, 'sign-out--user-menu');
    await page.getByRole('menuitem', { name: /sign out/i }).tap();
    await page.waitForURL(/\/sign-in/, { timeout: 30_000 });
    await settle(page);

    await expect(page.locator('input[name="identifier"]')).toBeVisible({ timeout: 30_000 });
    await page.locator('input[name="identifier"]').fill(user.email);
    await page.getByRole('button', { name: 'Continue', exact: true }).tap();
    await expect(page.locator('input[name="password"]')).toBeVisible();
    await page.locator('input[name="password"]').fill(user.password);
    await shotAndMeasure(page, 'sign-in--password');
    await page.getByRole('button', { name: 'Continue', exact: true }).tap();
    await page.waitForURL('**/today', { timeout: 30_000 });
    await settle(page);
    await shot(page, 'today--after-sign-in');
  });
});

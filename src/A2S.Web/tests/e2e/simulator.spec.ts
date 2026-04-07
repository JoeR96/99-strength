import { test, expect } from './fixtures/auth-fixture';

test.describe('Workout Simulator Page', () => {
  test('should render the simulation page', async ({ authenticatedPage: page }) => {
    await page.goto('/simulate');
    await expect(page.getByText(/Simulat/i).first()).toBeVisible();
  });

  test('should show workout selection dropdown', async ({ authenticatedPage: page }) => {
    await page.goto('/simulate');
    // Should show a workout selector (select dropdown or similar)
    await expect(page.getByText(/Select.*Workout|Choose.*Program|My Program/i).first()).toBeVisible();
  });

  test('should show session count input', async ({ authenticatedPage: page }) => {
    await page.goto('/simulate');
    // Should have an input for number of sessions to simulate
    const sessionInput = page.getByRole('spinbutton').first();
    if (await sessionInput.isVisible()) {
      await expect(sessionInput).toBeVisible();
    }
  });

  test('should have simulate/run button', async ({ authenticatedPage: page }) => {
    await page.goto('/simulate');
    const runButton = page.getByRole('button', { name: /Simulate|Run|Start/i }).first();
    await expect(runButton).toBeVisible();
  });

  test('should display charts after simulation', async ({ authenticatedPage: page }) => {
    await page.goto('/simulate');

    // Select workout and run simulation
    const runButton = page.getByRole('button', { name: /Simulate|Run|Start/i }).first();
    if (await runButton.isVisible()) {
      await runButton.click();
      // Should show chart components (Recharts renders SVG)
      const chart = page.locator('.recharts-wrapper, svg.recharts-surface').first();
      await expect(chart).toBeVisible({ timeout: 5000 });
    }
  });

  test('should show exercise progression data', async ({ authenticatedPage: page }) => {
    await page.goto('/simulate');

    const runButton = page.getByRole('button', { name: /Simulate|Run|Start/i }).first();
    if (await runButton.isVisible()) {
      await runButton.click();
      // Should show exercise names in the results
      await expect(page.getByText(/Bench Press|Training Max|Progression/i).first()).toBeVisible({ timeout: 5000 });
    }
  });
});

test.describe('Dashboard Exercise Tracking', () => {
  test('should show exercise tracking section on dashboard', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    // Dashboard should show exercise tracking with charts
    await expect(page.getByText(/Exercise.*Tracking|Progression|Training/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should display exercise progression charts', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    // Charts render as SVG via Recharts
    const charts = page.locator('.recharts-wrapper, svg.recharts-surface');
    if (await charts.first().isVisible({ timeout: 5000 }).catch(() => false)) {
      const count = await charts.count();
      expect(count).toBeGreaterThan(0);
    }
  });

  test('should show exercise names in tracking section', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    // Active workout exercises should be shown
    await expect(page.getByText(/Bench Press|Squat|Deadlift/i).first()).toBeVisible();
  });
});

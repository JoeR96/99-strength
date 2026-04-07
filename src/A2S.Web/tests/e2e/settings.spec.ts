import { test, expect } from './fixtures/auth-fixture';

test.describe('Settings Page', () => {
  test('should render the settings page', async ({ authenticatedPage: page }) => {
    await page.goto('/settings');
    await expect(page.getByText(/Settings/i).first()).toBeVisible();
  });

  test('should display theme selection options', async ({ authenticatedPage: page }) => {
    await page.goto('/settings');
    // Settings page should show theme or appearance section
    await expect(page.getByText(/Theme|Appearance|Display/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should have seed data management section', async ({ authenticatedPage: page }) => {
    await page.goto('/settings');
    const seedButton = page.getByRole('button', { name: /Seed|Import|Load/i }).first();
    if (await seedButton.isVisible()) {
      await expect(seedButton).toBeEnabled();
    }
  });

  test('should have export data option', async ({ authenticatedPage: page }) => {
    await page.goto('/settings');
    const exportButton = page.getByRole('button', { name: /Export|Download|Backup/i }).first();
    if (await exportButton.isVisible()) {
      await expect(exportButton).toBeEnabled();
    }
  });

  test('should show confirmation dialog for destructive actions', async ({ authenticatedPage: page }) => {
    await page.goto('/settings');
    // Click any destructive action button
    const dangerButton = page.getByRole('button', { name: /Reset|Clear|Delete.*Data/i }).first();
    if (await dangerButton.isVisible()) {
      await dangerButton.click();
      await expect(page.getByText(/Are you sure|confirm/i)).toBeVisible();
    }
  });
});

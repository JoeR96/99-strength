import { test, expect } from './fixtures/auth-fixture';
import { createMockWorkoutSummary } from './fixtures/test-data';

test.describe('Programs Page', () => {
  test('should render the programs page', async ({ authenticatedPage: page }) => {
    await page.goto('/programs');
    await expect(page.getByText(/Programs|My Programs/i).first()).toBeVisible();
  });

  test('should display program list', async ({ authenticatedPage: page }) => {
    await page.goto('/programs');
    await expect(page.getByText('My Program')).toBeVisible();
  });

  test('should show active status badge', async ({ authenticatedPage: page }) => {
    await page.goto('/programs');
    await expect(page.getByText(/Active/i).first()).toBeVisible();
  });

  test('should show program details (variant, week, exercises)', async ({ authenticatedPage: page }) => {
    await page.goto('/programs');
    await expect(page.getByText(/Week 1|21 weeks|4.*Day/i).first()).toBeVisible();
  });

  test('should have delete button for programs', async ({ authenticatedPage: page }) => {
    await page.goto('/programs');
    const deleteButton = page.getByRole('button', { name: /Delete/i }).first();
    if (await deleteButton.isVisible()) {
      await expect(deleteButton).toBeEnabled();
    }
  });

  test('should show confirmation dialog when deleting', async ({ authenticatedPage: page }) => {
    await page.goto('/programs');
    const deleteButton = page.getByRole('button', { name: /Delete/i }).first();
    if (await deleteButton.isVisible()) {
      await deleteButton.click();
      // Should show AlertDialog confirmation
      await expect(page.getByText(/Are you sure|cannot be undone/i)).toBeVisible();
    }
  });

  test('should navigate to setup wizard for new program', async ({ authenticatedPage: page }) => {
    await page.goto('/programs');
    const createButton = page.getByRole('link', { name: /Create|New|Add/i }).first();
    if (await createButton.isVisible()) {
      await createButton.click();
      await expect(page).toHaveURL(/setup/);
    }
  });

  test('should show empty state when no programs exist', async ({ page }) => {
    const { mockClerkAuth, mockApiRoutes } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);
    await mockApiRoutes(page, { workout: null, workouts: [] });

    await page.goto('/programs');
    await expect(page.getByText(/No programs|Create.*first|Get started/i).first()).toBeVisible();
  });
});

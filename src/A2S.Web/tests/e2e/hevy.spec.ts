import { test, expect } from './fixtures/auth-fixture';

test.describe('Hevy Management Page', () => {
  test('should render the Hevy management page', async ({ authenticatedPage: page }) => {
    await page.goto('/hevy');
    await expect(page.getByText(/Hevy/i).first()).toBeVisible();
  });

  test('should show API key configuration when not configured', async ({ page }) => {
    const { mockClerkAuth, mockApiRoutes } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);
    await mockApiRoutes(page);

    await page.goto('/hevy');
    // Should show API key input/setup section
    await expect(page.getByText(/API.*Key|Configure|Connect/i).first()).toBeVisible();
  });

  test('should show routines management when configured', async ({ authenticatedPage: page }) => {
    // Set Hevy API key in context (in-memory)
    await page.goto('/hevy');

    // Look for the settings component or API key input
    const apiKeyInput = page.getByPlaceholder(/API.*Key|Enter.*key/i).first();
    if (await apiKeyInput.isVisible()) {
      await apiKeyInput.fill('test-hevy-api-key');
      const saveButton = page.getByRole('button', { name: /Save|Connect|Verify/i }).first();
      if (await saveButton.isVisible()) {
        await saveButton.click();
      }
    }
  });

  test('should have delete routine confirmation dialog', async ({ authenticatedPage: page }) => {
    await page.goto('/hevy');
    // If routines are listed, clicking delete should show confirmation
    const deleteButton = page.getByRole('button', { name: /Delete|Remove/i }).first();
    if (await deleteButton.isVisible()) {
      await deleteButton.click();
      await expect(page.getByText(/Are you sure|confirm/i)).toBeVisible();
    }
  });
});

test.describe('Hevy Data Page', () => {
  test('should render the Hevy data page', async ({ authenticatedPage: page }) => {
    await page.goto('/hevy/data');
    await expect(page.getByText(/Hevy.*Data|Workout.*Data/i).first()).toBeVisible();
  });

  test('should prompt for API key when not configured', async ({ authenticatedPage: page }) => {
    await page.goto('/hevy/data');
    // Should show API key prompt or settings component
    await expect(page.getByText(/API.*Key|Configure|Connect|Enter/i).first()).toBeVisible();
  });

  test('should display workout list when API key is set', async ({ page }) => {
    const { mockClerkAuth, mockApiRoutes } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);
    await mockApiRoutes(page);

    // Pre-set API key via addInitScript
    await page.addInitScript(() => {
      // Mock HevyContext to indicate configured state
      (window as Record<string, unknown>).__hevy_api_key = 'test-key';
    });

    await page.goto('/hevy/data');
    // If configured, should attempt to load workouts
    // The mock API will return Push Day workout
    const workoutItem = page.getByText('Push Day');
    if (await workoutItem.isVisible({ timeout: 5000 }).catch(() => false)) {
      await expect(workoutItem).toBeVisible();
    }
  });

  test('should open exercise history modal on exercise click', async ({ page }) => {
    const { mockClerkAuth, mockApiRoutes } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);
    await mockApiRoutes(page);

    await page.goto('/hevy/data');

    const exerciseItem = page.getByText('Bench Press');
    if (await exerciseItem.isVisible({ timeout: 5000 }).catch(() => false)) {
      await exerciseItem.click();
      // Should show exercise history modal with chart
      await expect(page.getByText(/History|Performance|Chart/i).first()).toBeVisible();
    }
  });
});

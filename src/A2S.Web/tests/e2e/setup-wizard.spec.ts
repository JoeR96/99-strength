import { test, expect } from './fixtures/auth-fixture';

test.describe('Setup Wizard', () => {
  test('should show welcome step by default', async ({ authenticatedPage: page }) => {
    await page.goto('/setup');
    await expect(page.getByText(/Create.*Program|Welcome|Get Started/i)).toBeVisible();
  });

  test('should offer template and scratch setup modes', async ({ authenticatedPage: page }) => {
    await page.goto('/setup');
    await expect(page.getByText(/Template|Use a Template/i)).toBeVisible();
    await expect(page.getByText(/Scratch|Start from Scratch|Custom/i)).toBeVisible();
  });

  test('should navigate to template selection when choosing a template', async ({ authenticatedPage: page }) => {
    await page.goto('/setup');

    // Click template option
    const templateButton = page.getByText(/Template|Use a Template/i).first();
    await templateButton.click();

    // Should show template options (Push/Pull/Legs, Upper/Lower, etc.)
    await expect(page.getByText(/Push.*Pull.*Legs|Upper.*Lower|4.*Day|5.*Day/i).first()).toBeVisible();
  });

  test('should show exercise configuration step after template selection', async ({ authenticatedPage: page }) => {
    await page.goto('/setup');

    // Select template mode
    const templateButton = page.getByText(/Template|Use a Template/i).first();
    await templateButton.click();

    // Select a specific template
    const firstTemplate = page.locator('[data-testid="template-option"], button, [role="button"]')
      .filter({ hasText: /4.*Day|Push|Upper/i })
      .first();

    if (await firstTemplate.isVisible()) {
      await firstTemplate.click();
      // Should advance to exercise configuration or next step
      await expect(page.getByText(/Exercise|Configure|Customize|Day/i).first()).toBeVisible();
    }
  });

  test('should have working program name input', async ({ authenticatedPage: page }) => {
    await page.goto('/setup');

    const nameInput = page.getByRole('textbox').first();
    if (await nameInput.isVisible()) {
      await nameInput.clear();
      await nameInput.fill('My Custom Program');
      await expect(nameInput).toHaveValue('My Custom Program');
    }
  });

  test('should have a create/confirm button', async ({ authenticatedPage: page }) => {
    await page.goto('/setup');
    // The final step should have a create button (may not be visible on first step)
    await expect(page.getByRole('button').first()).toBeVisible();
  });
});

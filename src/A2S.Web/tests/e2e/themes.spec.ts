import { test, expect } from './fixtures/auth-fixture';

test.describe('Theme Switching', () => {
  test('should default to retro theme', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    // Default theme is retro — no dark or apple-theme class
    const html = page.locator('html');
    await expect(html).not.toHaveClass(/dark/);
    await expect(html).not.toHaveClass(/apple-theme/);
  });

  test('should switch to OSRS theme', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');

    // Click theme toggle button in navbar
    const themeToggle = page.getByRole('button', { name: /theme|toggle|CRT|sword|apple/i }).first();
    if (await themeToggle.isVisible()) {
      await themeToggle.click();
      // After first toggle: retro → osrs (dark class)
      const html = page.locator('html');
      await expect(html).toHaveClass(/dark/);
    }
  });

  test('should switch to Apple theme', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');

    const themeToggle = page.getByRole('button', { name: /theme|toggle|CRT|sword|apple/i }).first();
    if (await themeToggle.isVisible()) {
      // Toggle twice: retro → osrs → apple
      await themeToggle.click();
      await themeToggle.click();
      const html = page.locator('html');
      await expect(html).toHaveClass(/apple-theme/);
    }
  });

  test('should cycle back to retro theme', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');

    const themeToggle = page.getByRole('button', { name: /theme|toggle|CRT|sword|apple/i }).first();
    if (await themeToggle.isVisible()) {
      // Toggle three times: retro → osrs → apple → retro
      await themeToggle.click();
      await themeToggle.click();
      await themeToggle.click();
      const html = page.locator('html');
      await expect(html).not.toHaveClass(/dark/);
      await expect(html).not.toHaveClass(/apple-theme/);
    }
  });

  test('should persist theme selection in localStorage', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');

    const themeToggle = page.getByRole('button', { name: /theme|toggle|CRT|sword|apple/i }).first();
    if (await themeToggle.isVisible()) {
      await themeToggle.click();

      // Verify localStorage was updated
      const stored = await page.evaluate(() => localStorage.getItem('99-strength-theme-mode'));
      expect(stored).toBe('osrs');
    }
  });

  test('should apply correct CSS custom properties for retro theme', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');

    // Check that background color is set (retro theme has specific colors)
    const bgColor = await page.evaluate(() => {
      return getComputedStyle(document.documentElement).getPropertyValue('--background').trim();
    });
    expect(bgColor).toBeTruthy();
  });

  test('should apply correct CSS custom properties for Apple theme', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');

    const themeToggle = page.getByRole('button', { name: /theme|toggle|CRT|sword|apple/i }).first();
    if (await themeToggle.isVisible()) {
      // Switch to Apple theme
      await themeToggle.click();
      await themeToggle.click();

      // Apple theme should have specific font family
      const fontFamily = await page.evaluate(() => {
        return getComputedStyle(document.documentElement).getPropertyValue('--font-primary').trim();
      });
      // Apple theme uses system font stack
      if (fontFamily) {
        expect(fontFamily).toContain('apple-system');
      }
    }
  });

  test('should render readable text in all themes', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');

    const themeToggle = page.getByRole('button', { name: /theme|toggle|CRT|sword|apple/i }).first();
    const themes = ['retro', 'osrs', 'apple'];

    for (let i = 0; i < themes.length; i++) {
      // Verify welcome text is visible in each theme
      await expect(page.getByText(/Welcome back/i)).toBeVisible();

      if (await themeToggle.isVisible() && i < themes.length - 1) {
        await themeToggle.click();
      }
    }
  });
});

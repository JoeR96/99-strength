import { test, expect } from './fixtures/auth-fixture';
import { createMockWorkout, createMockWorkoutSummary } from './fixtures/test-data';

test.describe('Dashboard Page', () => {
  test('should display welcome message with user name', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    await expect(page.getByText(/Welcome back/i)).toBeVisible();
  });

  test('should show quick stats when active workout exists', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    await expect(page.getByText('Quick Stats')).toBeVisible();
    await expect(page.getByText('Total Workouts')).toBeVisible();
    await expect(page.getByText('This Week')).toBeVisible();
  });

  test('should show current program info', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    await expect(page.getByText('Current Program')).toBeVisible();
    await expect(page.getByText('My Program')).toBeVisible();
  });

  test('should show create program CTA when no active workout', async ({ page }) => {
    const { mockClerkAuth, mockApiRoutes } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);
    await mockApiRoutes(page, { workout: null, workouts: [] });

    await page.goto('/dashboard');
    await expect(page.getByText(/Create.*Program|Get Started|No active/i)).toBeVisible();
  });

  test('should show week overview for active workout', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    // Week overview renders days
    await expect(page.getByText(/Week 1/i)).toBeVisible();
  });

  test('should navigate to workout page from dashboard', async ({ authenticatedPage: page }) => {
    await page.goto('/dashboard');
    const workoutLink = page.getByRole('link', { name: /Workout/i }).first();
    await workoutLink.click();
    await expect(page).toHaveURL(/workout/);
  });

  test('should show loading state', async ({ page }) => {
    const { mockClerkAuth } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);

    // Delay API response to see loading
    await page.route('**/api/v1/workouts/active', async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 2000));
      await route.fulfill({ json: createMockWorkout() });
    });
    await page.route('**/api/v1/**', async (route) => {
      if (!route.request().url().includes('workouts/active')) {
        await route.fulfill({ json: {} });
      }
    });

    await page.goto('/dashboard');
    // Should show spinner or loading indicator
    await expect(page.locator('.animate-spin').first()).toBeVisible({ timeout: 2000 });
  });
});

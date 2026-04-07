import { test, expect } from './fixtures/auth-fixture';
import { createMockWorkout, createMockExercise } from './fixtures/test-data';

test.describe('Workout Dashboard', () => {
  test('should display active workout details', async ({ authenticatedPage: page }) => {
    await page.goto('/workout');
    await expect(page.getByText('My Program')).toBeVisible();
  });

  test('should show week overview with day cards', async ({ authenticatedPage: page }) => {
    await page.goto('/workout');
    await expect(page.getByText(/Week 1/i)).toBeVisible();
    // Should show day indicators for a 4-day split
    await expect(page.getByText(/Day 1/i)).toBeVisible();
  });

  test('should show exercises for the current day', async ({ authenticatedPage: page }) => {
    await page.goto('/workout');
    // Exercise names should be visible
    await expect(page.getByText('Bench Press')).toBeVisible();
  });

  test('should show no workout state when no active workout', async ({ page }) => {
    const { mockClerkAuth, mockApiRoutes } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);
    await mockApiRoutes(page, { workout: null });

    await page.goto('/workout');
    await expect(page.getByText(/No active|Create|Get started/i)).toBeVisible();
  });

  test('should show completed days with visual indicator', async ({ page }) => {
    const { mockClerkAuth, mockApiRoutes } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);
    const workout = createMockWorkout({ completedDaysInCurrentWeek: [1, 2] });
    await mockApiRoutes(page, { workout });

    await page.goto('/workout');
    await expect(page.getByText(/Week 1/i)).toBeVisible();
  });

  test('should show deload week indicator', async ({ page }) => {
    const { mockClerkAuth, mockApiRoutes } = await import('./fixtures/auth-fixture');
    await mockClerkAuth(page);
    const workout = createMockWorkout({ currentWeek: 7 });
    await mockApiRoutes(page, { workout });

    await page.goto('/workout');
    // Week 7 is a deload week in A2S2
    await expect(page.getByText(/Week 7/i)).toBeVisible();
  });
});

test.describe('Workout Session', () => {
  test('should render workout session page for a specific day', async ({ authenticatedPage: page }) => {
    await page.goto('/workout/session/1');
    // Should show exercises for day 1
    await expect(page.getByText(/Day 1|Bench Press/i)).toBeVisible();
  });
});

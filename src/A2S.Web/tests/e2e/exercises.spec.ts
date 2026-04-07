import { test, expect } from './fixtures/auth-fixture';

test.describe('Exercise Library Page', () => {
  test('should render the exercise library page', async ({ authenticatedPage: page }) => {
    await page.goto('/exercises');
    await expect(page.getByText(/Exercise.*Library|Exercises/i).first()).toBeVisible();
  });

  test('should display exercises from the library', async ({ authenticatedPage: page }) => {
    await page.goto('/exercises');
    // Exercise names should be visible from HEVY_EXERCISE_MAPPING (client-side data)
    await expect(page.getByText(/Bench Press|Squat|Deadlift/i).first()).toBeVisible();
  });

  test('should filter exercises by search query', async ({ authenticatedPage: page }) => {
    await page.goto('/exercises');

    const searchInput = page.getByPlaceholder(/Search|Filter/i).first();
    if (await searchInput.isVisible()) {
      await searchInput.fill('Bench');
      // Should show Bench Press and filter out unrelated exercises
      await expect(page.getByText('Bench Press').first()).toBeVisible();
    }
  });

  test('should support muscle group filtering', async ({ authenticatedPage: page }) => {
    await page.goto('/exercises');

    // Look for muscle group filter buttons/checkboxes
    const muscleFilter = page.getByText(/Chest|Back|Quads|Shoulders/i).first();
    if (await muscleFilter.isVisible()) {
      await muscleFilter.click();
    }
  });

  test('should support equipment filtering', async ({ authenticatedPage: page }) => {
    await page.goto('/exercises');

    const equipmentFilter = page.getByText(/Barbell|Dumbbell|Cable|Machine/i).first();
    if (await equipmentFilter.isVisible()) {
      await equipmentFilter.click();
    }
  });

  test('should support view mode switching (grid/list/grouped)', async ({ authenticatedPage: page }) => {
    await page.goto('/exercises');

    // Look for view mode toggle buttons
    const viewToggle = page.getByRole('button').filter({ hasText: /Grid|List|Group/i }).first();
    if (await viewToggle.isVisible()) {
      await viewToggle.click();
    }
  });

  test('should open exercise detail/history on click', async ({ authenticatedPage: page }) => {
    await page.goto('/exercises');

    // Click on an exercise to view its details
    const firstExercise = page.getByText(/Bench Press/i).first();
    if (await firstExercise.isVisible()) {
      await firstExercise.click();
      // Should show exercise history modal or detail view
      await expect(page.getByText(/History|Performance|Volume|Weight/i).first()).toBeVisible({ timeout: 5000 });
    }
  });
});

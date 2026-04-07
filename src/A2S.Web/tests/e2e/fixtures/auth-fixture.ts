import { test as base, type Page } from '@playwright/test';
import { TEST_USER, createMockWorkout, createMockWorkoutSummary, MOCK_EXERCISE_LIBRARY } from './test-data';

/**
 * Set up Clerk auth mocking. Intercepts Clerk's frontend API to simulate
 * an authenticated user without a real Clerk instance.
 */
export async function mockClerkAuth(page: Page) {
  // Intercept Clerk's frontend API calls
  await page.route('**/clerk**', async (route) => {
    const url = route.request().url();

    if (url.includes('/v1/client')) {
      await route.fulfill({
        json: {
          response: {
            object: 'client',
            id: 'client_test',
            sessions: [
              {
                object: 'session',
                id: 'sess_test',
                status: 'active',
                user: {
                  object: 'user',
                  id: TEST_USER.id,
                  first_name: TEST_USER.firstName,
                  last_name: TEST_USER.lastName,
                  email_addresses: [{ email_address: TEST_USER.email }],
                  primary_email_address_id: 'email_1',
                  image_url: TEST_USER.imageUrl,
                },
                last_active_token: {
                  jwt: 'mock-jwt-token',
                },
              },
            ],
            sign_in: null,
            sign_up: null,
            last_active_session_id: 'sess_test',
          },
        },
      });
      return;
    }

    if (url.includes('/v1/environment')) {
      await route.fulfill({
        json: {
          auth_config: { single_session_mode: true },
          display_config: {
            branded: false,
            application_name: '99 Strength',
            preferred_sign_in_strategy: 'password',
          },
          user_settings: {
            attributes: {
              email_address: { enabled: true, required: true },
              password: { enabled: true, required: true },
            },
          },
        },
      });
      return;
    }

    if (url.includes('/v1/me')) {
      await route.fulfill({
        json: {
          response: {
            object: 'user',
            id: TEST_USER.id,
            first_name: TEST_USER.firstName,
            last_name: TEST_USER.lastName,
            email_addresses: [{ email_address: TEST_USER.email }],
            image_url: TEST_USER.imageUrl,
          },
        },
      });
      return;
    }

    // Default: pass through or return empty
    await route.fulfill({ json: {} });
  });

  // Set Clerk session state via init script
  await page.addInitScript((user) => {
    // Stub Clerk's __clerk_frontend_api to prevent real API calls
    (window as Record<string, unknown>).__clerk_db_jwt = 'mock-jwt-token';
    (window as Record<string, unknown>).__clerk_session_id = 'sess_test';

    // Set localStorage items Clerk may check
    localStorage.setItem('__clerk_client_jwt', 'mock-jwt-token');
  }, TEST_USER);
}

/**
 * Set up standard API route mocks for all backend endpoints.
 */
export async function mockApiRoutes(page: Page, overrides: {
  workout?: ReturnType<typeof createMockWorkout> | null;
  workouts?: ReturnType<typeof createMockWorkoutSummary>[];
} = {}) {
  const workout = overrides.workout !== undefined ? overrides.workout : createMockWorkout();
  const workouts = overrides.workouts ?? [createMockWorkoutSummary()];

  // Mock active workout endpoint
  await page.route('**/api/v1/workouts/active', async (route) => {
    if (workout) {
      await route.fulfill({ json: workout });
    } else {
      await route.fulfill({ status: 404, json: { message: 'No active workout' } });
    }
  });

  // Mock workouts list
  await page.route('**/api/v1/workouts', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({ json: workouts });
    } else if (route.request().method() === 'POST') {
      await route.fulfill({ json: { id: 'new-workout-id' }, status: 201 });
    } else {
      await route.fulfill({ json: {} });
    }
  });

  // Mock individual workout endpoints
  await page.route('**/api/v1/workouts/*', async (route) => {
    const url = route.request().url();
    if (url.includes('/simulate')) {
      await route.fulfill({
        json: {
          workoutName: workout?.name ?? 'My Program',
          variant: workout?.variant ?? 'FourDay',
          startWeek: 1,
          endWeek: 5,
          totalWeeks: 5,
          exercises: (workout?.exercises ?? []).map((ex) => ({
            exerciseId: ex.id,
            exerciseName: ex.name,
            progressionType: ex.progression.type,
            dataPoints: Array.from({ length: 5 }, (_, i) => ({
              session: i + 1,
              week: i + 1,
              block: 1,
              trainingMax: 100 + i * 2.5,
              trainingMaxUnit: 'kg',
              currentWeight: 65 + i * 1.5,
              currentWeightUnit: 'kg',
              summary: { type: ex.progression.type, details: {} },
            })),
          })),
        },
      });
      return;
    }

    if (url.includes('/week-plan')) {
      await route.fulfill({
        json: {
          weekNumber: workout?.currentWeek ?? 1,
          days: [],
        },
      });
      return;
    }

    if (route.request().method() === 'GET') {
      await route.fulfill({ json: workout ?? {} });
    } else {
      await route.fulfill({ json: {} });
    }
  });

  // Mock exercise library
  await page.route('**/api/v1/exercises**', async (route) => {
    await route.fulfill({ json: MOCK_EXERCISE_LIBRARY });
  });

  // Mock exercise history
  await page.route('**/api/v1/exercise-history/**', async (route) => {
    await route.fulfill({
      json: {
        exerciseName: 'Bench Press',
        sessions: [
          { date: '2026-01-01', weight: 60, reps: 10, sets: 4, volume: 2400 },
          { date: '2026-02-01', weight: 65, reps: 10, sets: 4, volume: 2600 },
          { date: '2026-03-01', weight: 70, reps: 10, sets: 4, volume: 2800 },
        ],
      },
    });
  });

  // Mock users endpoint
  await page.route('**/api/v1/users/**', async (route) => {
    await route.fulfill({
      json: {
        id: TEST_USER.id,
        externalId: TEST_USER.id,
        email: TEST_USER.email,
        displayName: `${TEST_USER.firstName} ${TEST_USER.lastName}`,
      },
    });
  });

  // Mock Hevy proxy endpoints
  await page.route('**/api/v1/hevy/**', async (route) => {
    const url = route.request().url();

    if (url.includes('/data/workouts')) {
      await route.fulfill({
        json: {
          workouts: [
            {
              id: 'hevy-1',
              title: 'Push Day',
              startTime: '2026-03-01T10:00:00Z',
              endTime: '2026-03-01T11:30:00Z',
              exerciseCount: 5,
              exercises: [
                { exerciseTemplateId: 'bench', title: 'Bench Press', setCount: 4, bestWeight: 100, bestReps: 10, totalVolume: 3200 },
                { exerciseTemplateId: 'ohp', title: 'Overhead Press', setCount: 3, bestWeight: 60, bestReps: 8, totalVolume: 1440 },
              ],
            },
          ],
          page: 1,
          pageCount: 1,
        },
      });
      return;
    }

    if (url.includes('/data/exercises/')) {
      await route.fulfill({
        json: {
          exerciseName: 'Bench Press',
          dataPoints: [
            { date: '2026-01-15', avgWeight: 80, maxWeight: 85, totalVolume: 3200, avgReps: 10, totalSets: 4 },
            { date: '2026-02-15', avgWeight: 85, maxWeight: 90, totalVolume: 3400, avgReps: 10, totalSets: 4 },
          ],
        },
      });
      return;
    }

    // Generic Hevy proxy (routines, etc.)
    if (url.includes('/routines')) {
      await route.fulfill({ json: [] });
      return;
    }

    await route.fulfill({ json: {} });
  });
}

/**
 * Extended test fixture that auto-mocks Clerk auth and API routes.
 */
export const test = base.extend<{
  authenticatedPage: Page;
}>({
  authenticatedPage: async ({ page }, use) => {
    await mockClerkAuth(page);
    await mockApiRoutes(page);
    await use(page);
  },
});

export { expect } from '@playwright/test';

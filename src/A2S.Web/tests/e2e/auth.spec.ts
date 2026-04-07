import { test, expect } from '@playwright/test';
import { mockClerkAuth, mockApiRoutes } from './fixtures/auth-fixture';

test.describe('Authentication Flows', () => {
  test('should render sign-in page with Clerk UI', async ({ page }) => {
    // Don't mock auth — visit as unauthenticated user
    await page.route('**/clerk**', async (route) => {
      await route.fulfill({ json: {} });
    });

    await page.goto('/sign-in');
    await expect(page).toHaveURL(/sign-in/);
  });

  test('should render sign-up page', async ({ page }) => {
    await page.route('**/clerk**', async (route) => {
      await route.fulfill({ json: {} });
    });

    await page.goto('/sign-up');
    await expect(page).toHaveURL(/sign-up/);
  });

  test('should redirect unauthenticated users from protected routes to sign-in', async ({ page }) => {
    // Mock Clerk as unauthenticated
    await page.route('**/clerk**', async (route) => {
      const url = route.request().url();
      if (url.includes('/v1/client')) {
        await route.fulfill({
          json: {
            response: {
              object: 'client',
              id: 'client_test',
              sessions: [],
              sign_in: null,
              sign_up: null,
              last_active_session_id: null,
            },
          },
        });
        return;
      }
      await route.fulfill({ json: {} });
    });

    await page.goto('/dashboard');
    // ProtectedRoute should redirect to /sign-in
    await expect(page).toHaveURL(/sign-in/);
  });

  test('should redirect authenticated users from sign-in to dashboard', async ({ page }) => {
    await mockClerkAuth(page);
    await mockApiRoutes(page);

    await page.goto('/sign-in');
    // SignedIn component redirects to /dashboard
    await expect(page).toHaveURL(/dashboard/);
  });

  test('should allow authenticated users to access protected routes', async ({ page }) => {
    await mockClerkAuth(page);
    await mockApiRoutes(page);

    await page.goto('/dashboard');
    await expect(page).toHaveURL(/dashboard/);
    // Dashboard should show welcome message
    await expect(page.getByText(/Welcome back/i)).toBeVisible();
  });

  test('should redirect root path based on auth state', async ({ page }) => {
    await mockClerkAuth(page);
    await mockApiRoutes(page);

    await page.goto('/');
    // Authenticated user at root → /dashboard
    await expect(page).toHaveURL(/dashboard/);
  });
});

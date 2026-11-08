import { defineConfig, devices } from '@playwright/test';

// Storybook-driven mobile checks (brief § 6 "Playwright e2e on a mobile viewport"). These run
// against the built Storybook in storybook-static, served by scripts/storybook-harness.mjs, so
// they need neither the API nor a Clerk test user. Build first: npm run build-storybook -- --quiet
const port = Number(process.env.STORYBOOK_PORT ?? 6007);

export default defineConfig({
  testDir: './tests/storybook',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : undefined,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never', outputFolder: 'playwright-report/storybook' }]] : 'list',
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    trace: 'retain-on-failure',
    headless: true,
    // The brief's phone: 390 × 844 (iPhone 14-class), touch, 3x.
    ...devices['iPhone 14'],
    browserName: 'chromium',
    defaultBrowserType: 'chromium',
    viewport: { width: 390, height: 844 },
  },
  projects: [{ name: 'mobile-chromium' }],
  webServer: {
    command: `node scripts/storybook-harness.mjs`,
    url: `http://127.0.0.1:${port}/iframe.html`,
    reuseExistingServer: !process.env.CI,
    timeout: 30_000,
    env: { STORYBOOK_PORT: String(port) },
  },
});

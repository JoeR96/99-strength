import { defineConfig } from '@playwright/test';

// Live walkthrough (docs/v2/audit-after.md, "Live walkthrough"). Unlike the Storybook checks this
// drives the real stack: the API + Postgres from docker-compose.walkthrough.yml, a production web
// build served by `vite preview`, and a Clerk development-instance test user from .env.test.
// Nothing here starts a server and it is not part of `npm test`, `test:e2e` or `test:storybook`,
// so CI never runs it. How to bring the stack up, and what the run found, is in docs/v2/audit-after.md.
//
//   npx playwright test --config playwright.walkthrough.config.ts
export default defineConfig({
  testDir: './tests/walkthrough',
  outputDir: './test-results/walkthrough',
  // One signed-in browser profile is shared by every spec, and the specs build on each other
  // (sign up, start the plan, log a session, ...), so they run in file order on one worker.
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 180_000,
  expect: { timeout: 15_000 },
  reporter: 'list',
  use: {
    baseURL: process.env.WALKTHROUGH_WEB_URL ?? 'http://localhost:4173',
  },
});

# 99-Strength

Phone-first training tracker built around Greg Nuckols' Average to Savage 2 programme, with Hevy sync.
Architecture, domain model and conventions live in [`AGENTS.md`](AGENTS.md); the v2 "Whole Plan" brief,
audit and phase plan live under [`docs/v2/`](docs/v2/).

## Run it locally

```bash
npm start                 # Postgres (docker-compose) + API + Vite dev server
# or individually
npm run start:db
npm run start:api         # http://localhost:5123 — refuses to start with pending migrations
npm run start:web         # http://localhost:5173
```

Apply database migrations before starting the API:

```bash
dotnet ef database update -p src/A2S.Infrastructure -s src/A2S.Api
```

The Docker image applies them with an EF migration bundle in `docker-entrypoint.sh`.

## Install on your phone

99-Strength is a progressive web app: open it once in the browser and add it to your home screen.
It then launches full-screen, keeps you signed in and works for habits even when the signal drops.

**iPhone / iPad (Safari, iOS 16.4+)**
1. Open `https://99-strength.enjoeneer.dev` in Safari.
2. Tap **Share** (the square with the arrow), then **Add to Home Screen**, then **Add**.
3. Launch it from the Home Screen. Notifications only work from the installed app.

**Android (Chrome)**
1. Open `https://99-strength.enjoeneer.dev` in Chrome.
2. Tap the **⋮** menu, then **Install app** (or **Add to Home screen**), then **Install**.

## Tests

```bash
dotnet test A2S.sln                 # Domain, Application, Infrastructure (Testcontainers), API (Testcontainers)
cd src/A2S.Web && npm test          # Vitest
cd src/A2S.Web && npm run storybook # component states; Playwright screenshots use scripts/storybook-harness.mjs
```

Infrastructure and API tests need Docker running (they start `postgres:16-alpine` containers).

## Secrets

Never commit a Hevy API key, a Clerk secret or a Bitwarden session, and never paste one into a log,
test fixture, commit message or PR.

- **Users' Hevy keys** are encrypted at rest with ASP.NET Core Data Protection. The key ring is stored in
  the `DataProtectionKeys` table, so a redeploy keeps working. The API only ever returns
  `{ hasKey, lastFour }`; all calls to Hevy are made by the server.
- **Live Hevy smoke test** (`HevyLiveSmokeTests`) runs only when a key is present locally. Load it with
  user-secrets; nothing is written to the repo:

  ```bash
  dotnet user-secrets set "Hevy:LiveTestApiKey" "<your key>" --project src/A2S.Api
  ```

  The test project shares the API's `UserSecretsId`, so one command serves both. Alternatively set the
  `HEVY_LIVE_TEST_API_KEY` environment variable for the test run only. Remove it afterwards with
  `dotnet user-secrets remove "Hevy:LiveTestApiKey" --project src/A2S.Api`.
- **Web push (VAPID) keys** are read only from configuration: `Push:VapidPublicKey`, `Push:VapidPrivateKey`
  and `Push:Subject` (a `mailto:` address). Without all three the nudge service stays idle and Settings
  reports push as unavailable. Generate a pair once (for example `npx web-push generate-vapid-keys`) and
  keep it out of the repo:

  ```bash
  dotnet user-secrets set "Push:VapidPublicKey" "<public key>" --project src/A2S.Api
  dotnet user-secrets set "Push:VapidPrivateKey" "<private key>" --project src/A2S.Api
  dotnet user-secrets set "Push:Subject" "mailto:you@example.com" --project src/A2S.Api
  ```

  In production set the same three values as Dokploy environment variables (`Push__VapidPublicKey`,
  `Push__VapidPrivateKey`, `Push__Subject`). Rotating the pair invalidates every stored subscription;
  browsers re-subscribe the next time Settings is opened.
- **Frontend** configuration goes in an untracked `src/A2S.Web/.env.local` (see `.env.example`).

### Desktop widget tokens

A program that cannot sign in through Clerk, such as the Terran CC habit widget or a habit import
script, uses a personal API token instead. Create one in the app under
**Settings → Desktop widget tokens**, give it a name, and copy it straight away: it is shown once.
The server keeps only a SHA-256 hash, so a lost token cannot be recovered, only replaced.

Send it as `Authorization: Bearer nns_pat_…`. You can have ten active tokens.

A token can:

- call everything under `/api/v1/habits` (today, history, log, increment, create, edit, archive, import);
- record a weigh-in and read the trend under `/api/v1/body-weight`;
- read `GET /api/v1/plans/today`;
- call `GET /api/v1/users/me/api-tokens/whoami` to check that it works.

A token cannot do anything else. It cannot read or change the Hevy key, workouts, plans, meals or
push settings, and it cannot create, list or revoke tokens. Those calls answer `403`; an unknown or
revoked token answers `401`.

To revoke a token, press **Revoke** beside it in Settings. It stops working at once. Treat a token
like a password: keep it out of the repo, logs, commit messages and chat.

Adding a route to the list means marking it `[AllowApiToken]` and updating the list in
`tests/A2S.Api.Tests/Unit/ApiTokenAllowListTests.cs`, which fails until the two agree.

## Deploying a release

1. Back up production Postgres (`pg_dump --format=custom`) and rehearse the pending migrations against a
   restored copy.
2. Run the migration bundle (the Docker entrypoint does this), then deploy the API, then the frontend.
3. First boot after a schema change: the API upserts the exercise library by name, encrypts any legacy
   plaintext Hevy keys and erases the plaintext. Both steps are idempotent and only log counts. Rolling
   back to v1 after that boot means re-entering the Hevy key, because v1 reads the plaintext column.

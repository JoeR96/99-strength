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
- **Frontend** configuration goes in an untracked `src/A2S.Web/.env.local` (see `.env.example`).

## Deploying a release

1. Back up production Postgres (`pg_dump --format=custom`) and rehearse the pending migrations against a
   restored copy.
2. Run the migration bundle (the Docker entrypoint does this), then deploy the API, then the frontend.
3. First boot after a schema change: the API upserts the exercise library by name and encrypts any legacy
   plaintext Hevy keys. Both steps are idempotent and only log counts.

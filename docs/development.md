# Development

Running, testing, secrets and deployment. Moved out of the README, which is now user-facing.
Architecture and conventions live in [`AGENTS.md`](../AGENTS.md).

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

API tests share one test host whose current user is whichever `CreateAuthenticatedClient` call ran last
(`TestCurrentUserService` is static). In cross-user tests, create each client just before its requests;
see `PlansControllerTests.MobilityRoutines_AnotherUserCannotRenameOrDelete`.

Run the Storybook suite one branch at a time: `playwright.storybook.config.ts` reuses an existing server on
port 6007, so two worktrees running it together test each other's build. Set `STORYBOOK_PORT` to separate them.

## Gym mode

`/gym/:day` finishes through `POST /api/v1/gym-sessions` (`FinishGymSession`), idempotent on the
`clientSessionId` the phone makes at the start. Hevy posts are `HevyWorkoutPost` rows (migration
`AddHevyWorkoutPosts`); `HevyPostRetryHostedService` retries them every 3 minutes and is idle in the
`Testing` environment. Before re-posting, `HevyWorkoutPoster` adopts a Hevy workout with the same title and
a start within a minute, so a retry never creates a second one.

## Offline and saving

What the phone keeps, and how a write sent twice is still done once.

- **Offline queue** (`src/A2S.Web/src/offline`). `useOfflineMutation` tries the request; offline, or when it
  dies on the wire, the write is stored in IndexedDB (`a2s-offline`) as an op and replayed by `syncManager`.
  Op kinds: `habit.increment`, `habit.set`, `bodyweight.put`, `plan.complete`, `gym.finish`, `cardio.log`.
  Queries overlay pending ops (`overlay.ts`) so the screen shows server + pending; a track with queued logs
  shows **Waiting to sync**. A 4xx on replay drops the op with a toast (`reconcile.ts`).
- **Idempotency keys.** Each mutation call makes one key (nanoid) before the first attempt. It is sent as
  `Idempotency-Key` and becomes the op id, so the replay of a request whose answer was lost carries the same
  key. Server side, `[Idempotent]` on the action stores the first 2xx answer (status + JSON body) in
  `IdempotencyRecords` (migration `AddIdempotencyRecords`, unique on user + key, kept 7 days and purged on
  the next keyed write) and returns it for a repeat without running the action again. The same key with a
  different route or body answers 422. Gym finish (`clientSessionId`) and cardio logs (`clientKey` →
  `client:{key}`) also have natural keys, so they stay idempotent without the header.
- **Gym drafts** stay in localStorage (`a2s-gym-session:{workout}:{week}:{day}`), deliberately not IndexedDB.
  The shape is versioned (`GymSessionState.version`); `loadGymSession` upgrades older shapes through
  `migrateGymSession` (no version = v1) and discards only data it cannot read. `expireGymDrafts` removes
  drafts started more than 7 days ago (app start, `main.tsx`) and the current workout's drafts for earlier
  weeks (gym mode load).
- **Two devices.** Finishing a day that another device already finished (a different client session id)
  answers 409 "This day was already finished on another device" and applies no progression; the gym page
  shows it with **Discard this phone's session**.
- **Form drafts.** `useFormDraft({ key, version, value, initial })` writes `a2s-draft:{key}` while the form
  differs from where it started, offers `pendingDraft` on reopening (`DraftRestorePrompt`, "Restore unsaved
  changes?") and is cleared on save or Discard. Bump `version` when the form's shape changes; older drafts
  are dropped. `UnsavedChangesGuard` adds `beforeunload` and a react-router `useBlocker` dialog, which is why
  `App.tsx` uses `createBrowserRouter`. Used by `RoutineEditorPage` and the cardio track info sheet.

## Cardio tracks

Domain: `Aggregates/Cardio/CardioTrack.cs`, `CardioStrategies.cs`, `StageStrategy`, `CardioTrackTemplates.cs`,
persisted as jsonb in `CardioTracks` (migration `AddCardioTracks`). API `/api/v1/cardio-tracks`. Track
sessions are idempotent on a key (`completion:`, `hevy:{workout}:{exerciseIndex}`, `client:`, `manual:`), so
the Hevy pull and `CompleteSession` can run repeatedly. Finished cardio and mobility sessions post to Hevy
through `SessionHevyPoster` on the gym-mode `HevyWorkoutPost` queue; the pull uses
`CompleteSession(PostToHevy: false)` so nothing loops back. Built-in Hevy cardio template ids live in
`HevyTemplateCatalogue` (checked against `scripts/hevy-exercises.json`). Story mocks are in
`src/mocks/{fixtures,handlers}/cardioTracks.ts`; any story that renders `TodayCardioTracks` or
`CardioTrackProgress` must install `cardioTracksHandlers`.

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

## Clerk in the installed iPhone app

Sign-in must stay inside the standalone window, or the session cookie lands in Safari instead of the app.

- `LoginPage` and `SignUpPage` use `routing="path"` on our own origin (`/sign-in`, `/sign-up`) with
  `fallbackRedirectUrl="/today"`; nothing redirects to Clerk's hosted pages.
- The manifest `start_url` is `/today` (`display: standalone`, `scope: /`); `UserButton` signs out to `/sign-in`.
- Clerk's allowed origins must include production and the Vercel preview domain.

Device check, still to be recorded on a real iPhone: install from Safari, sign in (no Safari window opens),
force-quit and reopen (still signed in), background for 10 minutes and reopen, sign out (lands on `/sign-in`
inside the app). If the session is lost after force-quit, wire `ClerkProvider` to React Router
(`routerPush`/`routerReplace`, `signInUrl="/sign-in"`, `signUpUrl="/sign-up"`) and retest.

## Screenshots and walkthroughs

`scripts/capture-audit-screens.mjs` and the live walkthrough (`src/A2S.Web/tests/walkthrough/`) write to
`artifacts/`, which is gitignored. Attach screenshots to PRs or CI runs instead of committing them.

# v2 Phase 2 — Secrets, encryption and ground-clearing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Encrypt the Hevy API key at rest, keep it off the wire and out of logs, move every Hevy call to use the server-stored key, and make the test suites green on a fresh database.

**Architecture:** The `User` aggregate stores only a protected string and exposes `SetHevyApiKey(plaintext, ISecretProtector)` / `RevealHevyApiKey(ISecretProtector)`; the Domain owns the tiny `ISecretProtector` interface and Infrastructure implements it with ASP.NET Core Data Protection whose key ring lives in the database. Controllers and the sync handler resolve the key through `IHevyKeyResolver`; the browser never sends or receives it. A startup backfill encrypts legacy plaintext rows idempotently; the plaintext column is dropped in Phase 6.

**Tech Stack:** .NET 10, EF Core 9 packages, Npgsql, ASP.NET Core Data Protection (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`), Serilog, xUnit + Testcontainers; React 19, Vitest.

**Spec:** `docs/v2/plan.md` § "Phase 2" and `docs/v2/brief.md` § 3 (security).

## Global Constraints

- Never write the Hevy key or Bitwarden session to a committed file, fixture, log line, commit message, PR description or chat output.
- Clean Architecture: Domain has no package references; handlers stay thin; `Result<T>` for business failures.
- Migrations reversible; this phase is additive only (new column + new table). Plaintext column stays until Phase 6.
- A2S2 maths untouched.
- All existing suites green at the end: Domain, Application, Infrastructure, API (173 + new), Vitest.

## Review Focus

1. A legacy row whose plaintext key is present but whose protected column is null must be readable after one backfill run and unchanged after a second. Pinned in Task 4.
2. A protected value that cannot be unprotected (key ring lost) must surface as "no key" rather than crash every user load. Pinned in Task 2 (`Unprotect` returns null).
3. `PUT /users/me/hevy-api-key` with EF sensitive-data logging on must not put the plaintext into any log event. Pinned in Task 6 (capturing Serilog sink).
4. `GET /users/{id}` for another user's id must be 403, not 404 or 200. Pinned in Task 7.
5. Fresh database: migrate then seed yields the full 445-exercise library even though a migration already inserted one row. Pinned in Task 8.

---

### Task 1: Domain — protected key on `User`

**Files:**
- Create: `src/A2S.Domain/Common/ISecretProtector.cs`
- Modify: `src/A2S.Domain/Aggregates/User/User.cs`
- Test: `tests/A2S.Domain.Tests/Aggregates/User/UserHevyApiKeyTests.cs`

**Interfaces:**
- Produces: `interface ISecretProtector { string Protect(string plaintext); string? Unprotect(string protectedValue); }`
- Produces: `User.HevyApiKeyProtected : string?`, `User.HasHevyApiKey : bool`, `void SetHevyApiKey(string? apiKey, ISecretProtector protector)` (null/whitespace clears), `void ClearHevyApiKey()`, `string? RevealHevyApiKey(ISecretProtector protector)`.

- [ ] Write tests with a reversible fake protector (`"P:" + plaintext`): set stores protected not plaintext; reveal round-trips; whitespace clears; `HasHevyApiKey` flips.
- [ ] Run `dotnet test tests/A2S.Domain.Tests --filter UserHevyApiKeyTests` → compile failure.
- [ ] Implement; rename property, remove old `SetHevyApiKey(string?)`.
- [ ] Run again → PASS. Commit `feat(domain): store Hevy API key as a protected secret`.

### Task 2: Infrastructure — Data Protection protector, key ring in DB, migration

**Files:**
- Modify: `src/A2S.Infrastructure/A2S.Infrastructure.csproj` (add `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 9.0.0)
- Create: `src/A2S.Infrastructure/Security/DataProtectionSecretProtector.cs`
- Modify: `src/A2S.Infrastructure/Persistence/A2SDbContext.cs` (implements `IDataProtectionKeyContext`)
- Modify: `src/A2S.Infrastructure/Persistence/Configurations/UserConfiguration.cs` (map `HevyApiKeyProtected`, column type text)
- Modify: `src/A2S.Infrastructure/DependencyInjection.cs` (`AddDataProtection().SetApplicationName("99-strength").PersistKeysToDbContext<A2SDbContext>()`, `AddSingleton<ISecretProtector, DataProtectionSecretProtector>()`)
- Create: `src/A2S.Infrastructure/Migrations/<ts>_AddHevyApiKeyProtected.cs` (+ Designer, snapshot) — generated, then hand-edited so it does **not** drop `HevyApiKey`.
- Test: `tests/A2S.Infrastructure.Tests/Security/DataProtectionSecretProtectorTests.cs`

**Interfaces:**
- Produces: `DataProtectionSecretProtector(IDataProtectionProvider, ILogger<…>)`, purpose string `"A2S.User.HevyApiKey"`; `Unprotect` returns null on `CryptographicException`.

- [ ] Tests: round-trip with `EphemeralDataProtectionProvider`; unprotect of garbage returns null; protected value differs from plaintext.
- [ ] Generate migration: `A2S_CONNECTION_STRING="Host=localhost;Database=x;Username=x;Password=x" dotnet ef migrations add AddHevyApiKeyProtected -p src/A2S.Infrastructure -s src/A2S.Api`; remove the `DropColumn("HevyApiKey")` from Up and its `AddColumn` from Down.
- [ ] Run infra tests → PASS. Commit.

### Task 3: Application — `IHevyKeyResolver`, command no longer carries the key

**Files:**
- Create: `src/A2S.Application/Interfaces/IHevyKeyResolver.cs`
- Modify: `src/A2S.Application/Commands/SyncRoutineToHevy/SyncRoutineToHevyCommand.cs` (drop `HevyApiKey`), `…Handler.cs` (inject resolver; `Result.Failure("Hevy API key is required.", ErrorCode.Validation)` when null)
- Modify: `src/A2S.Application/Common/ICurrentUserService.cs` (remove `HevyApiKey`)
- Create: `src/A2S.Infrastructure/Security/UserHevyKeyResolver.cs`
- Modify: `tests/A2S.Application.Tests/Commands/SyncRoutineToHevyCommandHandlerTests.cs`, `tests/A2S.Tests.Shared/TestCurrentUserService.cs`

**Interfaces:**
- Produces: `interface IHevyKeyResolver { Task<string?> ForUserAsync(UserId userId, CancellationToken ct); Task<string?> ForCurrentUserAsync(CancellationToken ct); }`

- [ ] Handler tests: resolver returns null → failure containing "Hevy API key"; resolver returns key → passed to `SyncRoutineForDayAsync`.
- [ ] Implement; infra resolver loads the user and calls `RevealHevyApiKey`.
- [ ] Run Application tests → PASS. Commit.

### Task 4: Backfill of legacy plaintext rows

**Files:**
- Create: `src/A2S.Infrastructure/Security/HevyApiKeyBackfill.cs` — `static Task<int> RunAsync(A2SDbContext db, ISecretProtector protector, ILogger logger, CancellationToken ct)`; raw SQL: select `"Id","HevyApiKey"` where `"HevyApiKeyProtected" IS NULL AND "HevyApiKey" IS NOT NULL`; update protected; logs count only.
- Modify: `src/A2S.Api/Program.cs` (call after seeder)
- Test: `tests/A2S.Infrastructure.Tests/Security/HevyApiKeyBackfillTests.cs` (migrate a fresh DB in the fixture container, insert a user via SQL with plaintext, run → 1, reveal equals plaintext; run → 0).

- [ ] Write test, run (fails: type missing), implement, run → PASS. Commit.

### Task 5: API — key endpoints, server-side resolution, middleware cleanup

**Files:**
- Modify: `src/A2S.Api/Controllers/UsersController.cs` — `GET me/hevy-api-key` → `HevyApiKeyStatusResponse(bool HasKey, string? LastFour)`; `PUT` uses protector; `DELETE me/hevy-api-key` → 204; `GET {id}` 403 unless own id; remove `POST`.
- Modify: `HevyProxyController.cs`, `HevyDataController.cs`, `HevySyncController.cs` — remove `X-Hevy-Api-Key` parameters; resolve via `IHevyKeyResolver.ForCurrentUserAsync`; 400 "Hevy API key is required." when null.
- Modify: `AutoProvisionUserMiddleware.cs`, `CurrentUserService.cs` — remove `HevyApiKey` items.
- Modify: `src/A2S.Api/Program.cs` — register nothing new (resolver registered in Infrastructure).
- Tests: `UsersControllerTests.cs` (rewrite), `HevyProxyControllerTests.cs`, `HevyDataControllerTests.cs`, `HevySyncControllerTests.cs` (PUT key first instead of header).

- [ ] Tests: GET status before/after PUT (`hasKey`, `lastFour` = last 4 chars, body never contains the full key); DELETE clears; GET `{id}` own → 200, other → 403, unauth → 401; `POST /users` → 404/405.
- [ ] Implement; run API tests (they will still be red on the seeder until Task 8; run `--filter` on the touched classes).
- [ ] Commit.

### Task 6: Logging hygiene

**Files:**
- Modify: `src/A2S.Api/Program.cs` — `builder.Host.UseSerilog((ctx, services, cfg) => cfg.MinimumLevel.Information().MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Warning).Destructure.ByTransforming<User>(u => new { Id = u.Id.Value, u.Email, u.Name }).ReadFrom.Services(services).WriteTo.Console().WriteTo.File(...))`
- Modify: `src/A2S.Api/appsettings.Development.json` — `EnableSensitiveDataLogging: false`
- Test: `tests/A2S.Tests.Shared/CapturingLogSink.cs` (`ILogEventSink`, thread-safe list of `LogEvent`), registered in `TestWebApplicationFactory`; `tests/A2S.Api.Tests/Integration/HevyApiKeyLoggingTests.cs` — factory config sets `Database:EnableSensitiveDataLogging=true`; PUT a unique key; assert no captured event's rendered message, property values or exception contains it.

- [ ] Write test → fails on `CapturingLogSink` missing; implement; run → PASS. Commit.

### Task 7: Non-production endpoints

**Files:**
- Create: `src/A2S.Api/Infrastructure/NonProductionOnlyAttribute.cs` and `NonProductionActionsConvention.cs` (`IApplicationModelConvention` removing marked actions when `IHostEnvironment.IsProduction()`)
- Modify: `WorkoutsController.cs` (`[NonProductionOnly]` on `SimulateWorkout` and `SimulateStream`), `Program.cs` (`AddControllers(o => o.Conventions.Add(...))`)
- Test: `tests/A2S.Api.Tests/Unit/NonProductionActionsConventionTests.cs` — Production removes marked action, keeps unmarked; Development keeps both.

- [ ] Test → implement → PASS. Commit.

### Task 8: Seeder upsert + test, AGENTS.md, README, user-secrets

**Files:**
- Modify: `src/A2S.Infrastructure/SeedData/ExerciseDefinitionSeeder.cs` — load existing names, insert only missing; log inserted count.
- Test: `tests/A2S.Infrastructure.Tests/SeedData/ExerciseDefinitionSeederTests.cs` — fresh migrated DB → `Count == 445` and `Squat (Barbell)` exists; second run inserts 0.
- Modify: `AGENTS.md` (hard delete; migrations via bundle, not startup; proxy is the real Hevy path; key handling).
- Create: `README.md` (run, test, `dotnet user-secrets set "Hevy:LiveTestApiKey" "<key>" --project src/A2S.Api`, never commit).
- Modify: `src/A2S.Api/A2S.Api.csproj`, `tests/A2S.Api.Tests/A2S.Api.Tests.csproj` — `<UserSecretsId>99-strength-api</UserSecretsId>`.
- Create: `tests/A2S.Api.Tests/Integration/HevyLiveSmokeTests.cs` — `[LiveHevyFact]` skips unless `Hevy:LiveTestApiKey` is found in user-secrets or env; validates the key via `IHevyIntegrationService.ValidateApiKeyAsync`.

- [ ] Run full `dotnet test A2S.sln` → all green. Commit.

### Task 9: Frontend — key never in the browser

**Files:**
- Modify: `src/A2S.Web/src/contexts/HevyContext.tsx` (state `{hasKey,lastFour}`; `setApiKey` PUT then GET `/hevy/validate`; `clearApiKey` DELETE), `src/A2S.Web/src/services/hevyApi.ts` (drop header; `setConfigured(bool)` flag), `components/hevy/HevySettings.tsx` (masked `••••1234`, Replace / Remove), `features/exercises/ExerciseLibraryPage.tsx`, `features/hevy/HevyDataPage.tsx`, `features/hevy/HevyExerciseModal.tsx` (no apiKey prop/header), `features/settings/SettingsPage.tsx` (remove seed section, add `<HevySettings/>`), `api/users.ts` (remove `create`).
- Tests: `contexts/HevyContext.test.tsx`, `features/hevy/HevyDataPage.test.tsx`, `features/exercises/ExerciseLibraryPage.test.tsx` (mock `useHevy`), `features/workout/CompletionSummary.test.tsx` as needed.

- [ ] `npm test` → 0 failures; `npm run build` and `npm run lint` clean. Commit.

### Task 10: PR

- [ ] Push `feat/v2-secrets`; open PR to `master` titled "v2 Phase 2: encrypt Hevy key at rest, server-side key resolution, green test suites"; body lists the deploy note (backfill runs at startup; back up production first) and the Phase 6 column drop.

# v2 audit, after Phase 4 (draft)

Companion to `audit.md` § 2. Same method as the Phase 1 capture: Playwright against the static Storybook at 390 px, with the fixture data from the brief's seed (Mon Upper A, water 2/6, walks 1/2, weigh-in 94.6 kg, streak 5, zone 4–5 16 of 15–30). Screenshots are under `docs/v2/screens-after/`; regenerate them with:

```
cd src/A2S.Web && npm run build-storybook -- --quiet
node ../../scripts/capture-audit-screens.mjs
```

The harness serves the build locally, blocks every off-machine request and aliases Clerk to a stub, so the captures are deterministic and need no key. These are fixture screens, not a live walkthrough; Phase 9 repeats the journeys against the real API once a Clerk test user exists.

## Tap counts from app open

"App open" is the installed PWA's start URL, which is now `/today`. Counts are taps on the glass; typing a number is noted separately.

| Journey | Phase 1 (`audit.md`) | After Phase 4 | Evidence |
|---|---|---|---|
| Log a bottle of water | Not possible (no habits) | **1** (the 64 px `+` on the Water card) | `today--default.png` |
| Take a bottle off again | — | 1 (the `−`, or hold `+` for half a second) | `today--default.png` |
| Start today's lift | 1, but the button sat 1,900 px down the dashboard | 1, above the fold | `today--default.png` |
| Log this morning's weigh-in | Not possible | 1 tap + typing (field is open when nothing is logged; Edit when it is) | `today--empty.png`, `today--default.png` |
| Mark the 4×4 done | Not possible | 3 (Mark done, minutes, Mark done); zone 4–5 minutes optional | `today--cardio-day.png` |
| Tick Mobility A or Creatine | Not possible | 1 | `today--default.png` |
| Start the plan on first run | 8 taps through the wizard, on someone else's numbers | 1 tap + four training maxes, on Today itself | `today--no-plan.png` |
| See the week's goals and the streak | Not possible | 0 (on Today) | `today--default.png` |
| See weight against the checkpoints | Not possible | 1 (Progress tab) | `progress--on-pace.png`, `progress--behind.png` |
| See this week's hard sets against the caps | Not possible | 1 (Progress tab) | `progress--on-pace.png` |
| Add or edit a habit | Not possible | 2 (Habits tab, `+ New habit` / pencil) then the sheet | `habits--editing.png` |
| Undo a completed day | Landed on a route that did not exist | Returns to `/workout` | `useWorkoutSession.test.tsx` |

## Phone problems from § 2.8, status

- **No bottom navigation**: five tabs (Today / Plan / Habits / Progress / Settings), 56 px tall, on every signed-in page (PR 5), with Today and Progress now real pages.
- **Tall single columns**: Today is one screen plus the rings at 390 px (`today--default.png` is 2,440 px at 2× scale, so about 1,220 CSS px); Habits and Progress are each under three screens.
- **Tap targets under 44 px**: every new control is 44 px or larger by construction; the habit `+` is 64 px, Start is 56 px, the ring row is display only. Not re-measured by script in this pass.
- **Horizontal overflow**: none seen in the 23 captures.
- **Number inputs without `inputMode`**: weigh-in is `decimal`; cardio minutes, HR, RPE and zone 4–5 are `numeric`/`decimal`; habit target and minimum are `decimal`.
- **Session page** (§ 2.3): the six bugs are fixed and pinned by tests; a partial session can be finished; a deload week is announced; planned sets come from the API week plan.

## Still open after this PR

- Offline queueing and the pending-sync badge (PR 7): the badge slot exists (`today--offline-pending.png`) but nothing feeds it yet.
- "Open in Hevy" on the session card waits for the routine link (Phase 6); the button is hidden until then.
- Per-drill ticks in the mobility sheet wait for the routines API; today it is a single confirm.
- The axe pass from the Storybook a11y addon was not run headlessly in this capture.
- No live capture: the tap counts above are from the code paths and fixture screens.

---

## Live walkthrough (9 Nov 2026)

This was the first time the whole of v2 ran together: the API, Postgres, the production web build and real Clerk sign-in. **It did not get past sign-up.** Clerk's development instance asks every new sign-up to tick a Cloudflare "Verify you are human" box, and a script must not tick it. So no test user exists, and nothing behind sign-in was walked. The parts that need no sign-in were run in full, and they found real defects. Both halves are set out below.

### Environment

- Worktree `release/v2` on Windows 11, Docker Desktop 27.3.1, Node 24.20, .NET SDK 10.0.201.
- API and Postgres 16 from `docker-compose.yml` plus `docker-compose.walkthrough.yml`, project name `a2s-walkthrough`, `ASPNETCORE_ENVIRONMENT=Production`, API on `http://localhost:8080` (loopback only), a throwaway database password.
- Web: `npm run build` with the Clerk development publishable key and `VITE_API_BASE_URL=http://localhost:8080/api/v1`, served by `vite preview` on `http://localhost:4173`. This is the production bundle, so the service worker and manifest are the real ones.
- Browser: Playwright 1.60 driving Chromium 148, 390 × 844 viewport, touch on, device scale 2, Android Chrome user agent, Europe/London.
- Scripts: `src/A2S.Web/tests/walkthrough/`, run with `npx playwright test --config playwright.walkthrough.config.ts`. They are not part of `npm test`, `test:e2e` or `test:storybook`, so CI does not run them.
- Screenshots: `docs/v2/screens-live/`.

### What ran, and what it showed

| Check | Result |
|---|---|
| Empty database, entrypoint runs the migration bundle | All 12 migrations applied in order, `InitialCreate` to `AddApiTokens`, in about two seconds. 21 tables plus the migration history. |
| First boot | 449 exercises seeded (450 with the Pallof Press migration row), muscle groups filled in on 444, one Data Protection key created. |
| Restart on the same database | "No migrations were applied", no re-seeding, healthy within seconds. |
| Every API route with no session (96 routes) | All answer 401. None answer 200 or 5xx. |
| A made-up `nns_pat_` token and a made-up JWT | 401 on allowed and other routes alike. |
| Simulation routes and Swagger in Production | 404, as intended. |
| CORS | The web origin is allowed, a stranger origin is not, and a 401 still carries the CORS header. |
| Every path the web client calls (78 calls) against the live routes | 74 exist. Three do not: see defects 8 and 9. One was a false alarm from the scan. |
| Signed out, deep link to each of 19 routes | Each lands on `/sign-in` once. No blank page, no loop. |
| Manifest, icons, head tags | Correct. 192, 512 and maskable 512 icons are the sizes they claim; the apple-touch-icon is 180 × 180. |
| Service worker | Registers at `/sw.js`, controls the page, precaches `index.html` and the bundle. |
| Lighthouse 11.4 on `/sign-in` (the repo's `lighthouserc.json`, report kept local) | PWA 1.00 and every installability assertion passes. Performance 0.93. Best practices 0.74, then 0.78 after the fix to defect 5. |
| Offline cold start | The shell document comes from the precache, then **nothing is painted**. See defect 3. |
| API logs through all of the above | No unhandled exception. Warnings only, listed under defects 10, 11 and 14. |

### Sign-up: exactly what happened

1. `/sign-up` rendered. The script typed a `+clerk_test` address and a generated password and tapped Continue (`sign-up--filled.png`).
2. Clerk loaded Cloudflare Turnstile. The form was replaced by a "Verify you are human" checkbox (`sign-up--human-check.png`).
3. Clerk sent no `POST /v1/client/sign_ups`. No account was created. The fixed code `424242` was never asked for.
4. One Turnstile request to `brunhild.challenges.cloudflare.com` failed with `ERR_TUNNEL_CONNECTION_FAILED` on this machine. That may be why the check did not pass silently; it was not investigated further.

The test-email mechanism itself was never reached, so it is neither confirmed nor ruled out. `src/A2S.Web/.env.test` (gitignored) holds the address and password that were going to be used.

To finish the walkthrough, do one of these, then rerun the scripts:

- Run `WALKTHROUGH_HEADED=1 npx playwright test --config playwright.walkthrough.config.ts 02-sign-up` and tick the box yourself. The script types the rest and keeps the session in its browser profile.
- Or turn bot sign-up protection off for the development instance in the Clerk dashboard.

The signed-in scripts (start the plan, Today, a lifting session, cardio, habits, Progress, Plan, Meals, Settings, widget token, offline queue) are not written. Writing them blind, against screens that could not be opened, would only produce scripts nobody has seen pass.

### The friction points from `audit.md` § 2, live

"Storybook" means yesterday's fixture screens further up this page. "Not walked" means the screen sits behind sign-in.

| § | Friction point in v1 | Taps before | Taps now | Live status on 9 Nov |
|---|---|---|---|---|
| 2.1 | Sign-in card: all 9 controls under 44 px | — | — | **Walked.** Live it was 7 of 7 under 44 px (Continue 30 px, social buttons 32 px, links 18 px) and the GitHub button was invisible. Fixed: 1 of 7 remains, Clerk's own logo link. |
| 2.1 | No welcome or "start a plan" moment | 8 through the wizard | 1 + four numbers (Storybook) | Not walked. |
| 2.2 | Training maxes baked into templates; no start date | 8 | 1 + four numbers (Storybook) | Not walked. |
| 2.3 | Start button 1,900 px down the dashboard | 1, below the fold | 1, above the fold (Storybook) | Not walked. |
| 2.3 | 32 px inputs; no honest way to finish a partial session | — | — | Not walked. Covered by unit tests only. |
| 2.3 | Undo navigates to a route that does not exist | dead end | returns to `/workout` (unit test) | Not walked. |
| 2.4 | No calendar; deload not announced | — | — | Not walked. |
| 2.5 | A missed week is invisible | — | — | Not walked. |
| 2.6 | Substitution modal footers overflow at 390 px | — | — | Not walked. |
| 2.7 | Hevy settings on their own page; push from the browser | — | — | Not walked. But the old browser-side push is still wired to routes the API does not have: defect 9. |
| 2.8 | No bottom navigation | hamburger, scroll, tap | 1 (Storybook) | Not walked. |
| 2.8 | Tall pages, small targets, sideways scroll | — | — | Signed-out pages only: no sideways scroll, targets as in 2.1. |
| 2.8 | Number inputs without `inputMode` | — | — | Not walked. |
| 2.8 | No manifest, icons or offline behaviour | none | installable | **Walked.** Installable: yes. Offline from a cold start: no, defect 3. |
| — | Log a bottle of water | not possible | 1 (Storybook) | Not walked. |
| — | Mark the 4×4 done | not possible | 3 (Storybook) | Not walked. |

### Defects found

Severity: **high** stops a stated v2 goal or a deployment; **medium** is wrong and will bite; **low** is noise or polish.

| # | What | Where | Severity | Status |
|---|---|---|---|---|
| 1 | The API image built from a Windows checkout cannot start: `exec /app/docker-entrypoint.sh: no such file or directory`, restarting in a loop. The script had CRLF line endings. A Linux build host is not affected. | `docker-entrypoint.sh`, no `.gitattributes` | High on Windows, none on Linux | Fixed, `e4a95e5` |
| 2 | Sign-up needs a human check, so an automated test user cannot be created. | Clerk development instance (bot protection) | Blocks the walkthrough | Open. Joe's call, see above |
| 3 | Opening the app with no signal shows a blank screen. The shell is precached, but `clerk.browser.js` is fetched from Clerk's origin, fails, and neither `SignedIn` nor `SignedOut` renders. Seen live signed out. A signed-in user takes the same path before any session is known, so the installed app will be blank offline too; that part is read from the code, not seen. Offline habit taps can only work if the app was already open when the signal dropped. | `main.tsx` (`ClerkProvider`), `ProtectedRoute.tsx`, `sw.ts` | High: brief § 4 and § 6 ask for an offline shell | Open. Needs a decision on how to render without Clerk |
| 4 | With `Clerk__Domain` and `Cors__Origins__0` unset the API started, answered `/health` with 200, and then refused every signed-in request and every browser preflight. The "required" checks never fired because `appsettings.json` ships empty placeholders. | `Program.cs` | Medium | Fixed, `897aed8`. It now refuses to boot and names the variable |
| 5 | Every signed-out page load asked `GET /users/me/hevy-api-key`, got 401 and logged a console error. Lighthouse failed "errors in console" on it. | `HevyContext.tsx` | Low | Fixed, `ea1c960` |
| 6 | Clerk forms at 390 px: Continue 30 px tall, social buttons 32 px, show-password 40 × 24, Sign up / Sign in link 18 px; GitHub's black icon invisible on the dark card; a tall card flush against the top edge. | `clerkAppearance.ts`, `LoginPage.tsx`, `SignUpPage.tsx` | Low | Fixed, `cb721fc`. Clerk's logo link (48 × 14) stays |
| 7 | Chromium logs a deprecation warning on every load: only the `apple-` web-app meta tag is present. | `index.html` | Low | Fixed, `c1a371e` |
| 8 | Settings → More → Simulator opens a page whose only request is 404 in Production. | `SettingsPage.tsx` | Low | Fixed, `6ab34c4`. The link shows in development builds only |
| 9 | The client still calls `PUT /workouts/{id}/hevy-folder` and `POST /workouts/{id}/hevy-synced-routine`. Both answer 404; the API has never had them. They belong to the v1 browser-side Hevy push, which is still wired into the Plan tab (`WeekOverview`, `NextWeekPreview`, `hevySyncService`). With a key stored, those Sync buttons would create routines the v2 server-side push knows nothing about. The 404s were seen live; the buttons were not pressed. | `api/workouts.ts`, `features/workout/` | Medium | Fixed, `214d769`. The browser-built routine push is gone (Send buttons, the rebuild after an exercise edit, the swap at week rollover) and the Plan tab links to the push on Today and in Settings. The client now calls no route the API lacks, bar the simulator of defect 8. Still browser-driven through the proxy, each a decision: Send to Hevy on the completion summary, Pull from Hevy on the day card, the routine list on `/hevy`. Pushing next week ahead of time has no v2 button |
| 10 | Data Protection warns "No XML encryptor configured". The key ring sits unencrypted in the `DataProtectionKeys` table, in the same database as the Hevy keys it encrypts. A database dump contains both. | `DependencyInjection.cs` | Medium | Open. Needs a key outside the database |
| 11 | MediatR 14 logs at start-up that a licence key is required in production. | `A2S.Application.csproj` | Low, but a licensing question | Open |
| 12 | `/health` returns a fixed 200 and never touches the database, so the container health check cannot see a lost database. Read from the code. | `Program.cs` | Low | Open |
| 13 | `clerk-ios.md` named `/dashboard` as `start_url` and redirect; both are `/today`. | `docs/v2/clerk-ios.md` | Docs | Fixed, `6457239` |
| 14 | Log noise: a container health probe writes seven lines every 30 seconds, and a malformed bearer token writes a full stack trace at Information level. | Serilog request logging | Low | Open |
| 15 | Sign-in page copy is from v1: "Sign in to continue to your dashboard", "Powered by Average to Savage 2.0". | `LoginPage.tsx` | Low | Fixed, `335b791`. Now "Your whole training plan in one place", "Sign in to see what's on today", "Lifts, cardio, mobility, habits and weight". Copy is still Joe's to change |
| 16 | One 1.36 MB JavaScript chunk (381 kB gzipped) with no source map. Lighthouse marks both. | Vite build | Low | Open |

### Not exercised, and why

Everything behind sign-in, for the one reason above: first run and "Start this plan"; Today; a full and a partial lifting session, the AMRAP set, undo, the deload banner; cardio and mobility sessions and the zone 4–5 ring; Habits, long-press, streak, heatmap, editing; Progress; the Plan tab; Meals and the shopping list; Settings, Hevy not-connected states, push section, install instructions; desktop widget tokens (none was created, so the `whoami`, `habits/today` and 403 checks with a real token were not run); the offline queue and its single replay; reload and deep links while signed in.

One risk worth a look once a session exists, read from the code and not run: a 401 from the API sends the browser to `/sign-in`, and `/sign-in` sends a signed-in user back to `/today`. If the API ever rejects a session Clerk still thinks is good, that is a loop.

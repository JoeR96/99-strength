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

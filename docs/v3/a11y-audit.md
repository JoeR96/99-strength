# Accessibility audit (v3, phase 1)

**22 Nov 2026.** Every Storybook page story now passes the repo's mobile checks at 390 × 844: no sideways
overflow, every button and link at least 44 × 44 px, and no serious or critical axe violations
(WCAG 2.0/2.1 A and AA rules). The known-issues baseline in `tests/storybook/known-issues.ts` is now empty,
so any new defect fails CI.

Run: `npm run build-storybook -- --quiet`, then `STORYBOOK_NO_BASELINE=1 npm run test:storybook`.

## Results

| Check | Before | After |
|---|---|---|
| Page stories × 3 checks (overflow, tap targets, axe) | 25 failing of 219 | 0 failing of 244 |
| Stories failing tap targets | 19 (dashboard, exercise library, programs, setup wizard, workout dashboard) | 0 |
| Stories failing axe | 6 (setup wizard) | 0 |

The v2 screens (Today, Habits, Progress, Hevy inbox, Meals, Settings) already passed. Every failure was on
the older v1 screens.

## What was fixed

| Finding | WCAG | Fix |
|---|---|---|
| Small buttons were 40 px tall app-wide (`Button size="sm"`) | 2.5.8 (house rule 44 px) | `sm` is now 44 px (`h-11`) |
| Day card "Edit exercises" icon button 24 × 24 | 2.5.8 | 44 × 44 hit area |
| "Manage Blocks" 24 px tall | 2.5.8 | 44 px |
| Exercise library view toggles (36 px), muscle/equipment chips (26 px), "Clear all" (20 px), list rows (38 px) | 2.5.8 | 44 px minimum |
| Setup wizard equipment chips (30 px) and "Add" buttons (39 × 24) | 2.5.8 | 44 px minimum |
| Dashboard "Start workout": a button nested inside a link, 20 px tall | 4.1.2, 2.5.8 | One element (`Button asChild` around the `Link`) |
| Dashboard footer links (Help/Privacy/Terms) 20 px | 2.5.8 | 44 × 44 |
| Setup wizard step indicator scrolls but can't take focus | 2.1.1 (`scrollable-region-focusable`) | Focusable, labelled group with a focus ring |
| Setup wizard: Program name, Program variant and Total weeks had no programmatic label | 1.3.1, 4.1.2 (`label`, `select-name`) | `htmlFor`/`id` pairs |
| Sign-in and sign-up used `min-h-screen` (100vh): on iOS the keyboard and browser chrome can push the form or Continue button off screen | 1.4.10, 2.4.11 | Shared `AuthLayout`: `min-h-dvh`, top-aligned on phones |
| Sign-in and sign-up had no safe-area padding: content could sit under the status bar in the installed app | 1.4.10 | `env(safe-area-inset-*)` padding top and bottom |
| Sign-in used a different visual theme from sign-up and the app (gold/navy "Golden Twilight", 800 px blur blobs) | Consistency | Both screens share one frame in the app's orange theme; blobs removed |
| Stray duplicate shadcn component folder `src/A2S.Web/@/` (nothing imported it) | — | Deleted |

## Not covered yet (next phase)

- **Screens behind sign-in, live.** Stories cover the components, but no signed-in run against the real API has
  happened. It needs a Clerk test user (Testing Tokens or bot protection off on the dev instance).
- **Clerk's own form fields.** Clerk renders inside the card. The July audit measured its buttons; re-measure on a
  real phone.
- **Manual checks:** VoiceOver in the installed iPhone app, keyboard-only on desktop, iOS Larger Text and 200 % zoom,
  Reduce Motion, and the keyboard open on every form and bottom sheet.
- **WCAG 2.2-only rules** (2.4.11 focus not obscured by the tab bar, 2.5.7 drag alternatives, 3.3.8 paste in
  sign-in). axe's default tags don't check these; add `wcag22aa` to the axe run and review manually.
- **Charts** (weight trend, exercise history) need a text or table alternative.

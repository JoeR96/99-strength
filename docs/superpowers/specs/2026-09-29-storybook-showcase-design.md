# Storybook showcase — design

**Date:** 2026-09-29 · **Branch:** `feature/storybook-showcase` · **Scope:** `src/A2S.Web` only (no backend changes)

## Why

99 Strength is getting its own section on enjoeneer.dev. That section needs production-quality screenshots, and the
cleanest source for them is Storybook running the real pages against realistic mock data. Doing that exposes the
places where the app still drifts from the Arcade Minimal theme, so the theme gets tidied on the way.

## What changes

1. **Theme alignment.** Sweep every route page and shared component for off-contract styling (see `src/AGENTS.md`):
   - Golden Twilight leftovers on the login page (`bg-gradient-navy`, `text-gradient-gold`, `text-gold-light`, the
     `rgba(255,212,10,…)` grid and glow).
   - Raw Tailwind palette colours (`text-green-500`, `text-red-500`, `text-yellow-500`, `text-amber-500`,
     `text-white`, `ring-white`) → `success` / `destructive` / `warning` / `foreground` tokens.
   - Page shells and titles: pages mix `max-w-6xl mx-auto p-6`, `container mx-auto max-w-4xl`, `text-2xl`/`text-3xl`
     titles. Standardise on `.container-page py-8` and one shared `PageHeader` (`.text-hero` title + caption +
     optional actions).
   - One-off `<button>` styles (history toggles/export, settings actions, simulator chips) → `Button`.
   - Modal scrims vary between `bg-black/30…80` → one scrim value.
   - Stale comments that still describe Retro/OSRS/Apple/Golden Twilight themes.
   Behaviour stays the same.
2. **Login and sign-up.** Move both to the orange/black theme with a shared auth shell. Every text/background pair,
   including Clerk's labels, links, divider and footer, reaches WCAG AA (4.5:1 text, 3:1 large text/UI). Ratios are
   measured and written next to the values, as `index.css` already does. `clerkAppearance.ts` mirrors the tokens.
3. **Storybook on MSW.** Add `msw` + `msw-storybook-addon`, one handler set and one fixture set in `src/mocks/`:
   a lifter in week 11 of a 21-week, 4-day A2S program (training maxes, 10 weeks of history with AMRAP results and
   PRs, exercise history, a finished simulation run and its NDJSON stream, a connected Hevy account with synced
   routines and workouts). Clerk is swapped for a signed-in stub in Storybook via a Vite alias; the sign-in and
   sign-up stories use the real Clerk widget with the dev instance's publishable key (derived from the Clerk domain
   in `appsettings.Development.json`, publishable keys are public). A global decorator supplies QueryClient,
   MemoryRouter (per-story route) and HevyProvider.
   Stories: every route page (dashboard, workout, session, setup, programs, exercises, hevy, hevy/data, settings,
   history, simulate, sign-in, sign-up) and feature stories for the charts (ExerciseHistoryChart, simulation
   charts), the history calendar, setup wizard steps, set logging and exercise selection. Existing stories move to
   MSW where that makes them simpler.
4. **Screenshot pipeline.** `npm run showcase`: build Storybook, serve it, capture a curated list of stories with
   Playwright at 1440×900 (plus a 390×844 phone subset), save WebP under `showcase/` (< ~250 KB each), and a
   `showcase/README.md` describing each file.
5. **Accessibility audit.** axe-core (WCAG 2.x A/AA) over every story via Playwright. Fix violations, contrast
   first. Write `docs/superpowers/audits/2026-09-29-showcase-a11y-audit.md` with before/after.
6. **Verify and ship.** lint (no new errors), build, unit tests; PR to `master`.

## Small behaviour fixes found while reading

- `HevyProvider` loads a saved key on mount but never validates it, so `isValid` stays `null` after a reload and the
  Hevy page never lists routines ("Checking…" forever). Validate once after the key loads.
- `ExerciseLibraryPage.test.tsx` fails on `master` because it renders without a `HevyProvider`; mock it.

## Out of scope

Backend changes, new product features, splitting `SetupWizard.tsx`.

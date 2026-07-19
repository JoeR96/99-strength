# Frontend Flagship Restyle & Audit — Design

**Date:** 2026-07-18
**Status:** Approved approach (A: contract-first → audit → fix)
**Scope:** `src/A2S.Web` only. Purely visual/UX plus frontend code structure. **No business logic changes** — no edits to progression rules, API contracts, backend, or Hevy sync behavior.

## Goal

Replace the three-theme system with a single flagship look — **"Arcade Minimal"**: the Apple theme's clean, minimal design language carrying the Retro Arcade colour palette — then comprehensively audit and fix every screen against it at desktop and mobile breakpoints (weighted equally).

## 1. The flagship look ("Arcade Minimal")

### Palette

Taken from the current `:root` Retro Arcade tokens in `index.css`:

| Token | Value | Note |
|---|---|---|
| `--color-background` | `hsl(240 10% 4%)` | near-black |
| `--color-card` | `hsl(240 10% 10%)` | |
| `--color-foreground` | `hsl(0 0% 95%)` | near-white |
| `--color-primary` | `hsl(25 80% 45%)` | burnt orange (CSS comment says "olive"; it is orange — fix the comment) |
| `--color-accent` | `hsl(50 100% 50%)` | neon yellow |
| success / warning / destructive | existing retro values | unchanged |
| `--color-neon-*` | existing | reserved for charts / categorical use only, never UI chrome |

### Typography

- System font stack everywhere: `-apple-system, BlinkMacSystemFont, 'SF Pro Display'/'SF Pro Text', 'Inter', system-ui, sans-serif`.
- **Removed fonts:** Press Start 2P, VT323, Orbitron, RuneScape UF (imports, `@font-face`, and token overrides all deleted).
- A defined type scale replaces ad-hoc sizing: one page-title size, one section-heading size, body, caption. Exact utility classes chosen during contract rewrite and recorded in `src/AGENTS.md`.

### Surfaces, depth, spacing

- Apple-style restraint: 1px subtle borders, soft shadows, one consistent border radius.
- No scanline overlay, no pixel/CRT effects.
- One spacing rhythm (Tailwind 4/6/8 steps) for page padding, card padding, and stack gaps across all routes.

## 2. Theme consolidation (lands first — mostly deletion)

- `@theme` block in `index.css` becomes the single source of truth, updated to the hybrid values above.
- Delete: `.dark` (OSRS) block, `.apple-theme` block, theme font tokens/overrides, scanline CSS, theme switcher UI, `ThemeContext` cycling logic. If `ThemeContext` has no remaining purpose, delete it and its tests.
- `lib/chartTheme.ts` and `lib/blockColors.ts` continue to work unchanged (they read the same tokens).
- Rationale for ordering: every audit screenshot must show the real flagship, not a theme being deleted.

## 3. Audit method

### Live walkthrough (Playwright)

- Run the app locally (backend + `npm run dev`). Authenticate via Clerk as the **e2e test user from `src/A2S.Web/.env.test`** (email/password login — automatable, unlike the owner's Google-OAuth account); seed a workout via the setup wizard if the account has none, so real data renders.
- Drive all 12 routes: `/sign-in`, `/sign-up`, `/dashboard`, `/workout`, `/workout/session/:day`, `/setup`, `/programs`, `/exercises`, `/hevy`, `/hevy/data`, `/settings`, `/history`, `/simulate`.
- Also drive major modal flows: setup wizard steps, session logging, weight confirmation, exercise substitution, Hevy sync/review, progression tables.
- Screenshot every screen/state at **1440px (desktop)** and **390px (mobile)** — equal weight.

### Rubric

1. The new styling contract (section 1).
2. Fixed UX checklist: visual hierarchy/scannability, touch-target size (≥44px), empty/loading/error states, contrast (WCAG AA), focus states, layout breakage/overflow, consistency of repeated patterns (cards, tables, buttons, badges, modals).

### Static pass

- Token violations: hardcoded `#hex`/`hsl()`/`rgb()` colours, `dark:` variants, Tailwind arbitrary colour values, leftover theme conditionals.
- Structure (frontend only, behavior-preserving): files over the 500-line cap, components mixing styling/layout with orchestration (SRP), duplicated UI patterns that should be shared components. Refactors are extract-and-move only.

## 4. Deliverables & fix order

1. **Findings report** — every issue with screenshot evidence, severity (breaks-usability / inconsistent / polish), and screen reference.
2. **Fixes**, in order:
   1. Theme consolidation (section 2).
   2. Cross-cutting: shared components, layout shell, typography scale application.
   3. Screen-by-screen polish, worst-first.
   4. Structure refactors (SRP extractions, file splits).
3. **`src/A2S.Web/src/AGENTS.md` rewritten** — from a colour-token contract to the full flagship system: palette, typography scale, spacing rhythm, component patterns; all multi-theme language removed.

## 5. Verification

- Per fix chunk: `npm run build` + `npm test` green; Playwright re-screenshot of affected screens compared against the original finding.
- Theme-related tests updated or removed alongside the switcher.
- Final pass: full walkthrough re-run at both breakpoints to confirm no regressions.

## 6. Out of scope

Business logic, API contracts, backend, progression rules, Hevy sync behavior, new features. Storybook stories updated only where component APIs change.

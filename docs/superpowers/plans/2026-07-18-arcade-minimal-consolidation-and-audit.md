# Arcade Minimal — Theme Consolidation & Frontend Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Collapse the three-theme system into the single "Arcade Minimal" flagship theme (Apple minimal design language + Retro Arcade palette), rewrite the styling contract, then run a full Playwright-driven visual/UX audit of every route at 1440px and 390px producing a prioritized findings report.

**Architecture:** Pure-frontend work in `src/A2S.Web`. `index.css` (Tailwind v4 `@theme`) becomes the single source of styling truth; `ThemeContext` and the Navbar switcher are deleted. The audit is evidence-first: static greps plus live screenshots feed a findings document; fixes beyond consolidation are deliberately deferred to a Phase 2 plan authored from that document (Task 9) — do not improvise fixes during the audit.

**Tech Stack:** React 19, TypeScript 5.9, Vite 7, Tailwind CSS 4.1 (`@theme` tokens), Vitest, Playwright MCP, Clerk auth, .NET API + Postgres (docker-compose) for the live walkthrough.

**Spec:** `docs/superpowers/specs/2026-07-18-frontend-flagship-audit-design.md`

## Global Constraints

- **No business logic changes.** No edits to progression rules, API code, Hevy sync behavior, hooks' data logic, or backend. Structure refactors are extract-and-move only (and belong to Phase 2, not this plan).
- Colour references: Tailwind token utilities (`bg-primary`, `text-foreground`, …) or `var(--color-*)` only. Never `hsl(var(--…))` (double-wrap bug), never new hardcoded `#hex`/`hsl()`/`rgb()` in components.
- Max 500 lines per file.
- All commits on the current feature branch; message suffix `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Working directory for all `npm` commands: `src/A2S.Web`. All paths below are repo-relative.
- Screenshots go to `audit-screenshots/` (gitignored in Task 6); findings text is committed, screenshot binaries are not.

---

### Task 1: Rewrite `index.css` as the single Arcade Minimal theme

**Files:**
- Modify: `src/A2S.Web/src/index.css` (full replacement)
- Modify: `src/A2S.Web/src/features/history/WorkoutHistoryPage.tsx` (3× `container-apple`)
- Modify: `src/A2S.Web/src/features/settings/SettingsPage.tsx` (1× `container-apple`)
- Modify: `src/A2S.Web/src/features/auth/DashboardPage.tsx` (1× `container-apple`, 1× `theme-transition`)
- Modify: `src/A2S.Web/src/features/exercises/ExerciseLibraryPage.tsx` (1× `container-apple`, 1× `theme-transition`)
- Modify: `src/A2S.Web/src/components/layout/Navbar.tsx` (1× `container-apple`)

**Interfaces:**
- Produces: CSS utilities `.container-page`, `.text-hero`, `.text-body`, `.text-caption`; tokens `--color-*` (unchanged names), `--font-display`, `--font-text`, `--radius-*`, `--shadow-*`, `--duration-*`, `--ease-smooth`. All later tasks and Phase 2 style against these.
- Removed (nothing may reference these after this task): `.dark` / `.apple-theme` blocks, `--font-arcade`, `--font-osrs`, `.arcade-*`, `.neon-*`, `.glass`, `.pixel-text`, `.grid-bg`, `.dot-matrix`, `.hover-lift`, `.hover-scale`, `.fade-in-up`, `.neon-pulse`, `.theme-transition`, `.text-headline`, `.text-title`, `.text-white`, `.text-bright`, `.text-readable`, `.container-apple`, scanline `body::before`, external font `@import`s.

Verified usage facts (re-verify with Step 1 if the branch has moved): the only custom utility classes used in `.tsx` files are `container-apple` (7×), `theme-transition` (2×), `text-hero` (1×), `text-body` (2×), `text-caption` (8×). `font-display` Tailwind utility is used in `Navbar.tsx`. Nothing in `.ts`/`.tsx` uses arcade/neon/glass/pixel classes.

- [ ] **Step 1: Confirm the usage inventory is still current**

Run (from repo root):
```bash
grep -rnoE "container-apple|theme-transition|arcade-btn|arcade-card|arcade-stat|arcade-progress|neon-text|neon-border|pixel-text|glass|grid-bg|dot-matrix|hover-lift|hover-scale|fade-in-up|neon-pulse|text-headline|text-title|text-bright|text-readable" src/A2S.Web/src --include="*.tsx" --include="*.ts"
```
Expected: only `container-apple` and `theme-transition` hits, in the five files listed above. If anything else appears, add that occurrence to Step 3's replacements (map it to the nearest kept utility or plain Tailwind classes) before proceeding.

- [ ] **Step 2: Replace `src/A2S.Web/src/index.css` entirely with:**

```css
@import "tailwindcss";

@theme {
  /* ARCADE MINIMAL — the single flagship theme.
     Apple-style minimal language carrying the Retro Arcade palette. */

  /* Categorical accents — charts/data-viz only, never UI chrome */
  --color-neon-cyan: hsl(180 100% 50%);
  --color-neon-magenta: hsl(300 100% 60%);
  --color-neon-yellow: hsl(50 100% 50%);
  --color-neon-green: hsl(120 100% 45%);
  --color-neon-orange: hsl(30 100% 55%);
  --color-neon-pink: hsl(330 100% 65%);
  --color-neon-purple: hsl(270 100% 60%);
  --color-neon-blue: hsl(210 100% 55%);

  /* Semantic tokens */
  --color-background: hsl(240 10% 4%);
  --color-foreground: hsl(0 0% 95%);

  --color-card: hsl(240 10% 10%);
  --color-card-foreground: hsl(0 0% 95%);

  --color-popover: hsl(240 10% 10%);
  --color-popover-foreground: hsl(0 0% 95%);

  --color-primary: hsl(25 80% 45%); /* burnt orange */
  --color-primary-foreground: hsl(0 0% 100%);

  --color-secondary: hsl(240 10% 20%);
  --color-secondary-foreground: hsl(0 0% 95%);

  --color-muted: hsl(240 10% 16%);
  --color-muted-foreground: hsl(0 0% 65%);

  --color-accent: hsl(50 100% 50%); /* neon yellow */
  --color-accent-foreground: hsl(240 10% 4%);

  --color-destructive: hsl(0 100% 55%);
  --color-destructive-foreground: hsl(0 0% 100%);

  --color-success: hsl(120 100% 45%);
  --color-success-foreground: hsl(240 10% 4%);

  --color-warning: hsl(50 100% 50%);
  --color-warning-foreground: hsl(240 10% 4%);

  --color-border: hsl(0 0% 25%);
  --color-input: hsl(0 0% 20%);
  --color-ring: hsl(25 80% 45%);

  /* Radius — Apple-style rounding */
  --radius-sm: 0.5rem;
  --radius-md: 0.75rem;
  --radius-lg: 1rem;
  --radius-xl: 1.25rem;
  --radius-2xl: 1.5rem;
  --radius-full: 9999px;

  /* Shadows — soft, tuned for dark surfaces */
  --shadow-sm: 0 1px 2px hsl(0 0% 0% / 0.3);
  --shadow-md: 0 4px 6px hsl(0 0% 0% / 0.4);
  --shadow-lg: 0 10px 15px hsl(0 0% 0% / 0.4);
  --shadow-xl: 0 20px 25px hsl(0 0% 0% / 0.5);
  --shadow-lift: 0 10px 20px hsl(0 0% 0% / 0.4);

  /* Motion */
  --duration-fast: 100ms;
  --duration-normal: 150ms;
  --duration-slow: 250ms;
  --ease-smooth: cubic-bezier(0.25, 0.46, 0.45, 0.94);

  /* Fonts — system stack everywhere */
  --font-display: -apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Inter', system-ui, sans-serif;
  --font-text: -apple-system, BlinkMacSystemFont, 'SF Pro Text', 'Inter', system-ui, sans-serif;
}

@layer base {
  * {
    border-color: var(--color-border);
  }

  body {
    font-family: var(--font-text);
    font-size: 1rem;
    line-height: 1.6;
    letter-spacing: -0.01em;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
    background-color: var(--color-background);
    color: var(--color-foreground);
  }

  h1, h2, h3, h4, h5, h6 {
    font-family: var(--font-display);
    font-weight: 600;
    letter-spacing: -0.02em;
    color: var(--color-foreground);
  }

  h1 { font-size: 2.25rem; line-height: 1.2; }
  h2 { font-size: 1.75rem; line-height: 1.25; }
  h3 { font-size: 1.375rem; line-height: 1.3; }
  h4 { font-size: 1.125rem; line-height: 1.35; }

  ::selection {
    background: var(--color-primary);
    color: var(--color-primary-foreground);
  }

  ::-webkit-scrollbar {
    width: 8px;
    height: 8px;
  }

  ::-webkit-scrollbar-track {
    background: transparent;
  }

  ::-webkit-scrollbar-thumb {
    background: hsl(0 0% 30%);
    border-radius: 9999px;
  }

  ::-webkit-scrollbar-thumb:hover {
    background: hsl(0 0% 40%);
  }
}

@layer utilities {
  /* Page container */
  .container-page {
    max-width: 1200px;
    margin-inline: auto;
    padding-inline: 1.5rem;
  }

  @media (min-width: 1024px) {
    .container-page {
      max-width: 1400px;
    }
  }

  /* Type scale (see src/AGENTS.md — the styling contract) */
  .text-hero {
    font-family: var(--font-display);
    font-size: 2.25rem;
    font-weight: 700;
    line-height: 1.15;
    letter-spacing: -0.03em;
    color: var(--color-foreground);
  }

  .text-body {
    font-family: var(--font-text);
    font-size: 1rem;
    line-height: 1.6;
    color: var(--color-foreground);
  }

  .text-caption {
    font-family: var(--font-text);
    font-size: 0.875rem;
    line-height: 1.5;
    color: var(--color-muted-foreground);
  }
}
```

- [ ] **Step 3: Update the five component files**

In each file, do plain text replacements (use Edit with `replace_all` per file):
- `container-apple` → `container-page` (WorkoutHistoryPage ×3, SettingsPage ×1, DashboardPage ×1, ExerciseLibraryPage ×1, Navbar ×1)
- Remove the class token `theme-transition` (and any doubled space left behind) from `DashboardPage.tsx` and `ExerciseLibraryPage.tsx` — the class no longer exists and themes no longer switch.

- [ ] **Step 4: Verify no references to removed CSS remain**

```bash
grep -rnE "container-apple|theme-transition|font-arcade|pixel-text|arcade-btn|arcade-card|neon-text|\.glass" src/A2S.Web/src --include="*.tsx" --include="*.ts"
```
Expected: no matches.

- [ ] **Step 5: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; all tests pass (this task deletes no test subjects).

- [ ] **Step 6: Commit**

```bash
git add src/A2S.Web/src/index.css src/A2S.Web/src/features src/A2S.Web/src/components/layout/Navbar.tsx
git commit -m "Collapse index.css to single Arcade Minimal theme"
```

---

### Task 2: Remove the theme-switching machinery

**Files:**
- Delete: `src/A2S.Web/src/contexts/ThemeContext.tsx`
- Delete: `src/A2S.Web/src/contexts/ThemeContext.test.tsx`
- Modify: `src/A2S.Web/src/main.tsx`
- Modify: `src/A2S.Web/src/components/layout/Navbar.tsx`

**Interfaces:**
- Consumes: nothing from Task 1 (independent edits, but run after it so the `.dark`/`.apple-theme` classes it stops applying are already gone from CSS).
- Produces: no `ThemeProvider`/`useTheme` exports exist anywhere; `main.tsx` provider tree is `ClerkProvider > QueryClientProvider > HevyProvider`.

- [ ] **Step 1: Confirm the only consumers are Navbar and main.tsx**

```bash
grep -rln "ThemeContext\|useTheme\|ThemeProvider" src/A2S.Web/src --include="*.ts" --include="*.tsx"
```
Expected: exactly `contexts/ThemeContext.tsx`, `contexts/ThemeContext.test.tsx`, `components/layout/Navbar.tsx`, `main.tsx`. If more files appear, remove their theme usage the same way as Navbar's (delete the import and any `mode`-conditional branches, keeping the non-theme markup) before continuing.

- [ ] **Step 2: Edit `main.tsx`** — remove the import line `import { ThemeProvider } from './contexts/ThemeContext';` and unwrap the provider:

```tsx
      <QueryClientProvider client={queryClient}>
        <HevyProvider>
          <App />
          <Toaster
            position="bottom-right"
            toastOptions={{
              className: 'bg-background text-foreground border border-border',
              duration: 4000,
            }}
          />
        </HevyProvider>
        <ReactQueryDevtools initialIsOpen={false} />
      </QueryClientProvider>
```

- [ ] **Step 3: Edit `Navbar.tsx`** — three removals:
1. Delete `import { useTheme } from '@/contexts/ThemeContext';`
2. Delete `const { mode, toggleMode } = useTheme();`
3. Delete the entire "Theme Toggle" `<button>` block (the one with `onClick={toggleMode}`, `aria-label={`Switch theme…`}`, and the three conditional SVG icons — currently ~lines 108–131), including its `{/* Theme Toggle */}` comment.

- [ ] **Step 4: Delete the context files**

```bash
git rm src/A2S.Web/src/contexts/ThemeContext.tsx src/A2S.Web/src/contexts/ThemeContext.test.tsx
```

- [ ] **Step 5: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; test run passes with `ThemeContext.test` gone. If any other test fails on a missing `ThemeProvider`, that test was wrapping the provider — delete the wrapper (not the test) and re-run.

- [ ] **Step 6: Commit**

```bash
git add -A src/A2S.Web/src
git commit -m "Remove theme switcher and ThemeContext; single theme only"
```

---

### Task 3: Rewrite the styling contract (both AGENTS.md files)

**Files:**
- Modify: `src/A2S.Web/src/AGENTS.md` (full replacement)
- Modify: `src/A2S.Web/AGENTS.md` (Theming section only, currently the table + warning block under `### Theming`)

**Interfaces:**
- Consumes: token/utility names exactly as produced by Task 1.
- Produces: the audit rubric — Tasks 5 and 7 judge screens against this document; Phase 2 fixes cite its rules.

- [ ] **Step 1: Replace `src/A2S.Web/src/AGENTS.md` entirely with:**

```markdown
# UI & Styling Rules — A2S.Web/src

Scope: everything under `src/`. Inherits `../AGENTS.md` (stack, structure, conventions). This file is the **styling contract** for the single "Arcade Minimal" theme. Read it before adding or changing any colour, font, spacing, or chart.

## The theme

One theme: **Arcade Minimal** — Apple-style minimal design language carrying the Retro Arcade palette. Dark near-black surfaces, burnt-orange primary, neon-yellow accent, system fonts, subtle 1px borders, soft shadows. There is no theme switcher and no `.dark`/`.apple-theme` class; a `dark:` Tailwind variant is always dead code — remove it on sight.

## The token contract

All design tokens live in `index.css` as Tailwind v4 `@theme` custom properties. Each `--color-*` already holds a complete colour value (`--color-primary: hsl(25 80% 45%)`), so the **only** correct references are:

- ✅ Tailwind utilities: `bg-primary`, `text-foreground`, `border-border`, `text-muted-foreground`, `bg-card`, `text-destructive`, …
- ✅ Raw CSS var when a utility can't be used (SVG, inline style): `var(--color-primary)`
- ❌ `hsl(var(--color-primary))` or `hsl(var(--primary))` — double-wrapped → `hsl(undefined)` → black/illegible
- ❌ Literal `#hex` / `hsl(...)` / `rgb(...)` in components — including inline `style`, arbitrary values (`bg-[hsl(...)]`), and constant maps

For translucency use `color-mix(in srgb, var(--color-primary) 20%, transparent)`, never an alpha slot.

Fixed categorical palettes (values that must stay distinct) are centralised: `lib/chartTheme.ts` for chart series, `lib/blockColors.ts` for training-block identity. The `--color-neon-*` tokens exist **for charts/data-viz only** — never for UI chrome.

## Typography

System font stack only (`--font-display` for headings/`font-display` utility, `--font-text` for body). No decorative fonts, no `text-transform: uppercase` for headings, no letterspaced all-caps labels except tiny eyebrow captions.

Type scale (don't invent sizes):

| Role | Use |
|---|---|
| Page title | `.text-hero` (one per page) |
| Section heading | `h2`/`h3` element styles, or `text-xl`/`text-2xl font-semibold` |
| Body | `.text-body` or default body text |
| Caption / secondary | `.text-caption` or `text-sm text-muted-foreground` |

## Spacing & layout

- Page shell: `.container-page` + vertical `py-8` (pages), `space-y-6` between page sections.
- Cards: `p-6` padding (compact lists may use `p-4`), `gap-4` internal stacks.
- Stick to the 4/6/8 Tailwind steps for padding/gaps; anything else needs a reason.

## Surfaces & depth

- Cards/panels: `bg-card border border-border` + `rounded-lg` (tokens give Apple-style radii). Shadows only for elevation that means something (modals `shadow-lg`, popovers `shadow-md`); resting cards need no shadow.
- No glows, scanlines, pixel effects, or gradient backgrounds.

## Charts (Recharts)

Recharts renders SVG and cannot consume Tailwind classes, so colours must be concrete strings. Always import from `lib/chartTheme.ts` — never inline colour strings in a chart:

- Single-series chart (even with a metric switcher): `chartColors.primary` for line, dots, legend.
- Multi-series: `chartSeriesPalette[i % chartSeriesPalette.length]`.
- Always set `tick={{ fill: chartColors.mutedForeground }}` on axes — `stroke` alone leaves tick text illegible.
- Tooltip: `contentStyle={chartTooltipContentStyle}`.

## Components

- ShadCN primitives in `components/ui/` for buttons, cards, dialogs, inputs. Compose these rather than re-styling raw elements.
- `cn()` from `lib/utils` for conditional class merging.
- Max 500 lines/file (see parent `AGENTS.md`).
- Touch targets ≥ 44px on interactive elements.

## Known debt (don't replicate; fix opportunistically)

_Tracked in `docs/superpowers/audits/2026-07-18-frontend-audit-findings.md` during the flagship audit; migrate leftovers here when that effort closes._
```

- [ ] **Step 2: Update the parent `src/A2S.Web/AGENTS.md`** — replace the `### Theming` section (the three-theme table, the `ThemeContext` cycling sentence, and the `⚠️` warning paragraph) with:

```markdown
### Theming

**One theme: "Arcade Minimal"** — Apple-style minimal design language with the Retro Arcade palette (near-black background, burnt-orange primary, neon-yellow accent, system fonts). All tokens live in `src/index.css` (`@theme`). There is no theme switcher.

> ⚠️ The colour CSS variables are named `--color-*` (Tailwind v4 `@theme`) and **already contain a full `hsl(...)` value**. Reference them as `var(--color-primary)` (or the `bg-primary` / `text-foreground` utilities) — **never** `hsl(var(--primary))`, which resolves to `hsl(undefined)` and renders black/illegible. Charts use `src/lib/chartTheme.ts`. See **`src/AGENTS.md`** for the full styling contract — read it before touching any colour.
```

Also remove `ThemeContext` from the contexts line in the Project Structure block if it's listed there (`contexts/         # React contexts (ThemeContext, HevyContext)` → `contexts/         # React contexts (HevyContext)`).

- [ ] **Step 3: Commit**

```bash
git add src/A2S.Web/AGENTS.md src/A2S.Web/src/AGENTS.md
git commit -m "Rewrite styling contract for single Arcade Minimal theme"
```

---

### Task 4: Static audit sweep (token violations + structure)

**Files:**
- Create: `docs/superpowers/audits/2026-07-18-frontend-audit-findings.md`

**Interfaces:**
- Produces: the findings document with sections `## Static findings` and an empty `## Screen findings` skeleton. Tasks 6–8 append to it. Finding format (used by every later task):
  `- **[SEV]** \`file-or-screen\` — description. Evidence: \`grep line\` or \`screenshot-name.png\`.`
  where SEV ∈ `P1` (breaks usability/legibility), `P2` (inconsistent with contract), `P3` (polish).

- [ ] **Step 1: Create the findings file with this skeleton:**

```markdown
# Frontend Audit Findings — Arcade Minimal

**Date started:** 2026-07-18
**Rubric:** `src/A2S.Web/src/AGENTS.md` (styling contract) + UX checklist in spec §3.
**Severity:** P1 breaks usability/legibility · P2 violates contract/inconsistent · P3 polish.

## Static findings

### Hardcoded colours in components

### Dead `dark:` variants

### Arbitrary Tailwind colour values

### Files over 500 lines

### Structure / SRP observations

## Screen findings

_One subsection per route/flow, appended during the Playwright walkthrough (Task 6)._
```

- [ ] **Step 2: Run the sweeps and record every hit under the matching section** (repo root; record `file:line — snippet` per hit, then classify severity):

```bash
# Hardcoded colours (exclude index.css which owns tokens, and test files)
grep -rnE "#[0-9a-fA-F]{6}\b|hsl\(\s*[0-9]|rgb\(" src/A2S.Web/src --include="*.tsx" --include="*.ts" | grep -v ".test." | grep -v "index.css"

# Dead dark: variants (no .dark class exists any more)
grep -rn "dark:" src/A2S.Web/src --include="*.tsx"

# Arbitrary colour values
grep -rnE "\[(#|hsl|rgb)" src/A2S.Web/src --include="*.tsx"

# Files over 500 lines
find src/A2S.Web/src -name "*.tsx" -o -name "*.ts" | xargs wc -l | awk '$1 > 500 {print}' | sort -rn
```

- [ ] **Step 3: Structure pass** — for each file over 500 lines and each file flagged by the colour sweeps, skim and note (don't fix): mixed responsibilities (data orchestration + heavy markup in one component), duplicated UI patterns (e.g. repeated badge/stat-card markup that should be a shared component), inline style objects that belong in a primitive. One bullet per observation under `### Structure / SRP observations`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/audits/2026-07-18-frontend-audit-findings.md
git commit -m "Add static audit findings for Arcade Minimal"
```

---

### Task 5: Bring up the full local stack

**Files:**
- Modify: `.gitignore` (add `audit-screenshots/`)
- Create: `audit-screenshots/` (directory, untracked)

**Interfaces:**
- Produces: Postgres on `localhost:5432`, API on `http://localhost:5123`, frontend on `http://localhost:5173`, logged-in Clerk session for the `.env.test` test user. Task 6 depends on all four.

- [ ] **Step 1: Add `audit-screenshots/` to `.gitignore`** (new line at end of the repo-root `.gitignore`), create the directory, and commit the `.gitignore` change:

```bash
mkdir -p audit-screenshots
git add .gitignore
git commit -m "Ignore audit screenshot directory"
```

- [ ] **Step 2: Start Postgres**

```bash
docker compose up -d
docker compose ps
```
Expected: postgres service `running` on 5432.

- [ ] **Step 3: Start the API** (background)

```bash
dotnet run --project src/A2S.Api
```
Expected in output: `Now listening on: http://localhost:5123`. Verify: `curl -k -s -o /dev/null -w "%{http_code}" http://localhost:5123/api/v1/workouts/current` → `401` (unauthenticated, but server up).

- [ ] **Step 4: Start the frontend** (background)

```bash
cd src/A2S.Web && npm run dev
```
Expected: `Local: http://localhost:5173/`.

- [ ] **Step 5: Log in via Playwright MCP** — navigate to `http://localhost:5173/sign-in`, fill the Clerk email/password form with `TEST_USER_EMAIL` / `TEST_USER_PASSWORD` from `src/A2S.Web/.env.test` (read the file for the values; never paste them into the findings doc or commit messages). Expected: redirect to `/dashboard`.

- [ ] **Step 6: Ensure the account has workout data** — visit `/workout`. If it shows an active workout, done. If not, complete the setup wizard: create a workout from the **Big Daves Bonanza** template (`big-daves-bonanza`) accepting defaults, so Dashboard/Workout/History/Simulator render real content. (Creating a workout is user-level app usage, not a business logic change.)

---

### Task 6: Playwright walkthrough — screenshot every route and flow at both breakpoints

**Files:**
- Modify: `docs/superpowers/audits/2026-07-18-frontend-audit-findings.md` (append `## Screen findings` subsections)
- Create: `audit-screenshots/*.png` (untracked evidence)

**Interfaces:**
- Consumes: running stack + session from Task 5; finding format from Task 4.
- Produces: for every walk item, screenshots named `<slug>--1440.png` / `<slug>--390.png` and a findings subsection `### <slug>`.

- [ ] **Step 1: Walk list — for each item: set viewport 1440×900, navigate/open, screenshot, set viewport 390×844, screenshot, then write findings.** Slugs and targets:

| slug | target |
|---|---|
| `sign-in` | `/sign-in` (use a logged-out browser context/incognito for this row and `sign-up`) |
| `sign-up` | `/sign-up` |
| `dashboard` | `/dashboard` |
| `workout` | `/workout` |
| `workout-session` | `/workout/session/1` (or the current day shown on `/workout`) |
| `setup-wizard-1..n` | `/setup` — one screenshot pair per wizard step; cancel at the end without creating a second workout |
| `programs` | `/programs` |
| `exercises` | `/exercises` |
| `hevy` | `/hevy` |
| `hevy-data` | `/hevy/data` |
| `settings` | `/settings` |
| `history` | `/history` |
| `simulate` | `/simulate` |
| `modal-progression` | progression tables modal (open from a workout exercise) |
| `modal-substitution` | exercise substitution modal |
| `modal-weight-confirm` | weight confirmation dialog if reachable without completing a session; otherwise note "not reached" in findings |
| `nav-mobile` | mobile hamburger menu open, 390px only |

- [ ] **Step 2: Judge each screen against the rubric while looking at it** — for every issue write a finding line immediately (don't batch from memory). Checklist per screen: page title/hierarchy scannable? spacing on the 4/6/8 rhythm? cards/buttons/badges consistent with other screens? text contrast ≥ WCAG AA (spot-check muted text on `bg-card`)? touch targets ≥44px at 390px? overflow/clipping/horizontal scroll at 390px? empty/loading/error states styled (throttle or use a bogus route param to trigger where cheap)? focus visible when tabbing? leftover retro styling (uppercase display headings, glows) clashing with Arcade Minimal?

- [ ] **Step 3: Commit findings after every 4–5 screens** (screenshots stay untracked):

```bash
git add docs/superpowers/audits/2026-07-18-frontend-audit-findings.md
git commit -m "Audit findings: <screens covered>"
```

---

### Task 7: Verify consolidation did not regress the app

**Interfaces:**
- Consumes: walkthrough evidence from Task 6.

- [ ] **Step 1: Confirm from the Task 6 screenshots**: no screen renders black-on-black or otherwise illegible; fonts are the system stack everywhere (no pixel/serif leftovers — if a screen still shows Press Start 2P/VT323/RuneScape glyphs, a hardcoded `font-family` exists: locate with `grep -rn "Press Start\|VT323\|Orbitron\|RuneScape" src/A2S.Web/src` and record as a P1 finding); no scanline overlay visible.

- [ ] **Step 2: Full check suite**

```bash
cd src/A2S.Web && npm run build && npm test && npm run lint
```
Expected: all green. Record any lint failures that pre-date this work as findings instead of fixing them here.

---

### Task 8: Prioritize and finalize the findings report

**Files:**
- Modify: `docs/superpowers/audits/2026-07-18-frontend-audit-findings.md`

- [ ] **Step 1: Add a `## Summary & fix order` section at the top** listing: counts per severity; the P1 list in full; recommended fix grouping per spec §4 — (a) cross-cutting (shared components / typography-scale application / spacing rhythm), (b) screen-by-screen worst-first, (c) SRP/structure refactors — with each finding assigned to a group.

- [ ] **Step 2: Self-review the report**: every finding has severity + evidence; no finding proposes a business logic change; screen coverage matches the Task 6 walk list (note any `not reached` rows explicitly).

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/audits/2026-07-18-frontend-audit-findings.md
git commit -m "Finalize prioritized frontend audit findings"
```

---

### Task 9: Author the Phase 2 fix plan

- [ ] **Step 1: Invoke the `superpowers:writing-plans` skill** with the finalized findings report as input to produce `docs/superpowers/plans/<date>-arcade-minimal-fixes.md`, honoring the fix order from Task 8 and the Global Constraints above (no business logic changes; extract-and-move refactors only). Present that plan to the user for approval before executing it.

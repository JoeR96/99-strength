# Arcade Minimal — Phase 2 Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve the findings from the `2026-07-18` frontend audit against the single "Arcade Minimal" theme. Fix the seven P1 usability/legibility breaks and every P2 contract violation via cross-cutting root-cause fixes (Group A), then screen-specific residuals (Group B), then behavior-preserving structure refactors (Group C). Each fix satisfies the styling contract in `src/A2S.Web/src/AGENTS.md`.

**Architecture:** Pure-frontend work in `src/A2S.Web`. `index.css` (Tailwind v4 `@theme`) remains the single styling source of truth. Root causes are fixed once in shared primitives (`components/ui/button.tsx`, a new `components/ui/badge.tsx`, a new `components/shared/ReviewModal.tsx`, a new `components/shared/ConfirmModal.tsx`, a new `lib/outcomeStatus.ts`) and consumed everywhere, so per-screen findings collapse together. A committed Playwright re-capture tool (`tools/audit-capture.mjs`) drives verification: re-screenshot the affected routes at 1440×900 and 390×844 after each change and eyeball against the original finding.

**Tech Stack:** React 19, TypeScript 5.9, Vite 7, Tailwind CSS 4.1 (`@theme` tokens), Vitest, `@playwright/test` (already in `src/A2S.Web/node_modules`), Clerk auth, .NET API + Postgres for the live walkthrough.

**Spec / Inputs:**
- Findings + fix order: `docs/superpowers/audits/2026-07-18-frontend-audit-findings.md` (the `## Summary & fix order` section is the task-ordering authority).
- Styling contract: `src/A2S.Web/src/AGENTS.md`.
- Prior plan (header/format conventions): `docs/superpowers/plans/2026-07-18-arcade-minimal-consolidation-and-audit.md`.

## Global Constraints

- **No business logic changes.** No edits to progression rules, API code, Hevy sync behavior, hooks' data logic, or backend. Structure refactors (Group C) are extract-and-move only.
- Colour references: Tailwind token utilities (`bg-primary`, `text-foreground`, …) or `var(--color-*)` only. Never `hsl(var(--…))` (double-wrap bug), never new hardcoded `#hex`/`hsl()`/`rgb()` in components.
- Max 500 lines per file.
- All commits on the current feature branch; message suffix `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Working directory for all `npm` commands: `src/A2S.Web`. All paths below are repo-relative.
- Screenshots go to `audit-screenshots/` (already gitignored); findings text is committed, screenshot binaries are not.
- **3 pre-existing test failures in `ExerciseLibraryPage.test.tsx` and 60 pre-existing lint errors are the accepted baseline — do not fix unless a task names them; never introduce new ones.** (`npm test` baseline: 246/249 passing. `npm run lint` baseline: 60 errors.)

---

### Task 1: Create the committed re-screenshot tool `tools/audit-capture.mjs`

**Files:**
- Create: `tools/audit-capture.mjs`
- Modify: `.gitignore` (add `tools/.auth-state.json`)

**Interfaces:**
- Produces: a Playwright capture script that logs in via Clerk, saves storage state to `tools/.auth-state.json`, and screenshots a caller-provided list of routes at 1440×900 and 390×844 into `audit-screenshots/`. Every Group B and the final task invoke it.

**Precondition (document, do not automate here):** the full local stack must already be running before invoking the tool. Bring it up with:
```bash
docker start a2s-audit-pg
dotnet run --project src/A2S.Api --launch-profile http
cd src/A2S.Web && npm run dev
```
The tool assumes API on `http://localhost:5123` and frontend on `http://localhost:5173`.

- [ ] **Step 1: Add the auth-state file to `.gitignore`** — append a line `tools/.auth-state.json` to the repo-root `.gitignore` (after the existing `audit-screenshots/` line). The screenshot tool is committed; the saved Clerk session token is not.

- [ ] **Step 2: Create `tools/audit-capture.mjs` with exactly this content:**

```js
// Re-screenshot audited routes at desktop + mobile for Phase 2 verification.
//
// Precondition: the full stack is running —
//   docker start a2s-audit-pg
//   dotnet run --project src/A2S.Api --launch-profile http
//   (cd src/A2S.Web && npm run dev)
//
// Usage (from repo root):
//   node tools/audit-capture.mjs                       # capture the default route set
//   node tools/audit-capture.mjs /dashboard /workout   # capture only these routes
//
// Screenshots land in audit-screenshots/ as <slug>--1440.png / <slug>--390.png.
// A logged-in Clerk session is persisted to tools/.auth-state.json and reused.

import { chromium } from '../src/A2S.Web/node_modules/@playwright/test/index.js';
import { readFileSync, existsSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..');
const authStatePath = resolve(__dirname, '.auth-state.json');
const shotsDir = resolve(repoRoot, 'audit-screenshots');
const FRONTEND = 'http://localhost:5173';

// Read TEST_USER_EMAIL / TEST_USER_PASSWORD from src/A2S.Web/.env.test
function loadEnvTest() {
  const envPath = resolve(repoRoot, 'src/A2S.Web/.env.test');
  const raw = readFileSync(envPath, 'utf8');
  const env = {};
  for (const line of raw.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eq = trimmed.indexOf('=');
    if (eq === -1) continue;
    const key = trimmed.slice(0, eq).trim();
    let val = trimmed.slice(eq + 1).trim();
    if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
      val = val.slice(1, -1);
    }
    env[key] = val;
  }
  return env;
}

// Default route set: every audited screen reachable without a completed session.
const DEFAULT_ROUTES = [
  ['dashboard', '/dashboard'],
  ['workout', '/workout'],
  ['programs', '/programs'],
  ['exercises', '/exercises'],
  ['hevy', '/hevy'],
  ['hevy-data', '/hevy/data'],
  ['settings', '/settings'],
  ['history', '/history'],
  ['simulate', '/simulate'],
  ['setup', '/setup'],
];

function slugFor(route) {
  const clean = route.replace(/^\//, '').replace(/\//g, '-');
  return clean || 'root';
}

async function ensureAuthState(browser, env) {
  if (existsSync(authStatePath)) return;
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto(`${FRONTEND}/sign-in`, { waitUntil: 'networkidle' });
  await page.fill('input[name="identifier"]', env.TEST_USER_EMAIL);
  await page.getByRole('button', { name: /continue/i }).first().click();
  await page.fill('input[name="password"]', env.TEST_USER_PASSWORD);
  await page.getByRole('button', { name: /continue/i }).first().click();
  await page.waitForURL('**/dashboard', { timeout: 30000 });
  await context.storageState({ path: authStatePath });
  await context.close();
}

async function capture(browser, routes) {
  for (const width of [1440, 390]) {
    const height = width === 1440 ? 900 : 844;
    const context = await browser.newContext({
      storageState: authStatePath,
      viewport: { width, height },
    });
    const page = await context.newPage();
    for (const [slug, route] of routes) {
      await page.goto(`${FRONTEND}${route}`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(600);
      await page.screenshot({
        path: resolve(shotsDir, `${slug}--${width}.png`),
        fullPage: true,
      });
      console.log(`captured ${slug}--${width}.png`);
    }
    await context.close();
  }
}

async function main() {
  if (!existsSync(shotsDir)) mkdirSync(shotsDir, { recursive: true });
  const env = loadEnvTest();
  if (!env.TEST_USER_EMAIL || !env.TEST_USER_PASSWORD) {
    throw new Error('TEST_USER_EMAIL / TEST_USER_PASSWORD missing from src/A2S.Web/.env.test');
  }

  const argRoutes = process.argv.slice(2);
  const routes = argRoutes.length
    ? argRoutes.map((r) => [slugFor(r), r])
    : DEFAULT_ROUTES;

  const browser = await chromium.launch();
  try {
    await ensureAuthState(browser, env);
    await capture(browser, routes);
  } finally {
    await browser.close();
  }
  console.log('done');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
```

- [ ] **Step 3: Smoke-test the tool** (stack must be running per the precondition):

```bash
node tools/audit-capture.mjs /dashboard
```
Expected output: `captured dashboard--1440.png`, `captured dashboard--390.png`, `done`. Confirm `audit-screenshots/dashboard--1440.png` and `dashboard--390.png` exist, and `tools/.auth-state.json` was created.

- [ ] **Step 4: Confirm the auth-state file is ignored**

```bash
git status --porcelain tools/.auth-state.json
```
Expected: no output (file is ignored).

- [ ] **Step 5: Commit**

```bash
git add tools/audit-capture.mjs .gitignore
git commit -m "Add committed audit re-screenshot tool" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Group A — cross-cutting root-cause fixes

Executed in the findings doc's stated order (most downstream findings unblocked first).

---

### Task 2 (Group A-1): Remove Orbitron + `uppercase tracking-wide` from `button.tsx` and drop the banned `glow` variant

Resolves the cross-screen retro-button findings: static root-cause (`button.tsx:8`), cross-screen 139/141/142 (button clause), dashboard 147, workout 154, workout-session 160, modal-progression 175, modal-substitution 181, setup-wizard-1 202, setup-wizard-2 209, setup-wizard-3 219, setup-wizard-4 226, programs 234, exercises button treatment, hevy 248, hevy-data 254, simulate 272, settings 259, history 266. Also removes the banned `glow` variant.

**Files:**
- Modify: `src/A2S.Web/src/components/ui/button.tsx`

**Interfaces:**
- Produces: a `buttonVariants` base string with no `uppercase`, no `tracking-wide`, no `font-[Orbitron,sans-serif]`; token-only foreground/border colours (`text-primary-foreground`, `text-foreground`, `border-border`, `text-muted-foreground`) replacing `text-white`/`border-gray-500`/`text-gray-300`; and no `glow` variant. Every `<Button>` call site inherits the fix.

- [ ] **Step 1: Replace the entire `buttonVariants` declaration** (lines 7-61) with:

```tsx
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap font-semibold transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        // Primary button - flat burnt-orange fill, legible foreground
        default:
          "bg-primary text-primary-foreground border border-primary hover:bg-primary/90 active:translate-y-0.5 transition-all duration-150",

        // Destructive button - red
        destructive:
          "bg-destructive text-destructive-foreground border border-destructive hover:bg-destructive/90 active:translate-y-0.5 transition-all duration-150",

        // Outlined button - clean border
        outline:
          "border border-border bg-transparent text-foreground hover:bg-foreground/5 hover:border-foreground/40 transition-all duration-150",

        // Secondary button - subtle dark
        secondary:
          "bg-secondary text-secondary-foreground border border-secondary hover:bg-secondary/80 active:translate-y-0.5 transition-all duration-150",

        // Ghost button - minimal
        ghost:
          "text-muted-foreground hover:bg-foreground/10 hover:text-foreground transition-all duration-150",

        // Link button
        link:
          "text-primary underline-offset-4 hover:underline transition-all duration-150",

        // Success button - green
        success:
          "bg-success text-success-foreground border border-success hover:bg-success/90 active:translate-y-0.5 transition-all duration-150",

        // Accent button - yellow
        accent:
          "bg-accent text-accent-foreground border border-accent hover:bg-accent/90 active:translate-y-0.5 transition-all duration-150",
      },
      size: {
        default: "h-12 px-6 py-3 text-base rounded-md",
        sm: "h-10 px-4 py-2 text-sm rounded",
        lg: "h-14 px-8 py-3 text-lg rounded-md",
        xl: "h-16 px-10 py-4 text-xl rounded-lg",
        icon: "h-12 w-12 rounded-md",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)
```

Notes: the `shadow-md`/`shadow-lg` classes are dropped from resting buttons per the contract ("resting cards need no shadow"); the removed `glow` variant is the banned `shadow-primary/25→/40` one. The primary now uses `text-primary-foreground` (`hsl(0 0% 100%)`, full white) on the flat `bg-primary` fill — this is the fill/foreground retune that also addresses the "washed-out label on orange" clause; the separate CONNECT HEVY dark-brown fill is handled in Task 3.

- [ ] **Step 2: Confirm no `glow` variant remains referenced**

```bash
grep -rn 'variant="glow"\|variant={"glow"}\|"glow"' src/A2S.Web/src --include="*.tsx"
```
Expected: no matches (the variant was unused at call sites; only the definition existed).

- [ ] **Step 3: Confirm the banned tokens are gone from button.tsx**

```bash
grep -nE "Orbitron|uppercase|tracking-wide|text-white|border-gray-500|text-gray-300|shadow-primary" src/A2S.Web/src/components/ui/button.tsx
```
Expected: no matches.

- [ ] **Step 4: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; `npm test` 246/249 (baseline; the 3 `ExerciseLibraryPage.test.tsx` failures unchanged, no new failures).

- [ ] **Step 5: Commit**

```bash
git add src/A2S.Web/src/components/ui/button.tsx
git commit -m "Remove Orbitron/uppercase from Button base variant; drop glow variant" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3 (Group A-2): Fix CONNECT HEVY dark-brown fill + label contrast (P1×2)

Resolves the two P1 contrast failures: hevy 247 and hevy-data 253 ("CONNECT HEVY renders as a dark muted-brown fill with grey-on-brown label text … reads as disabled"). The finding names a distinct fill/foreground defect that the Task 2 font fix does NOT resolve.

**Files:**
- Modify: the CONNECT HEVY button (find it in the Hevy feature — locate with the grep in Step 1)

**Interfaces:**
- Produces: the CONNECT HEVY CTA rendered with the default primary variant (flat `bg-primary` + `text-primary-foreground`) and no overriding dark/muted fill or grey foreground class.

- [ ] **Step 1: Locate the CONNECT HEVY button and its current classes**

```bash
grep -rniE "connect hevy" src/A2S.Web/src --include="*.tsx" -l
```
Then read the matched file(s) and find the button rendering "Connect Hevy". Record the exact `className`/`variant` on that `<Button>` and the surrounding wrapper. (The dark-brown fill comes from a non-token colour class — e.g. a `bg-*` override, an inline `style`, or a muted variant — applied on top of or instead of the primary variant.)

- [ ] **Step 2: Fix the fill and foreground** — on the CONNECT HEVY `<Button>`, remove any `bg-*`/`text-*`/inline-`style` colour override so it uses the default primary variant (`bg-primary text-primary-foreground`). If a disabled/loading state legitimately dims it, gate that on the actual `disabled` prop (which already applies `disabled:opacity-50` from the base variant) rather than a hardcoded muted fill. Do not change the button's onClick/handler or any data logic — colour classes only.

- [ ] **Step 3: Confirm no off-token colour class remains on the CONNECT HEVY button**

```bash
grep -rniE "connect hevy" src/A2S.Web/src --include="*.tsx" -A2 -B2 | grep -iE "bg-\[|bg-amber|bg-orange-9|bg-yellow-9|text-gray|text-zinc|#|hsl\(|rgb\("
```
Expected: no matches on the button.

- [ ] **Step 4: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 246/249 (baseline).

- [ ] **Step 5: Re-screenshot and eyeball** (stack running):

```bash
node tools/audit-capture.mjs /hevy /hevy/data
```
Expected: `captured hevy--1440.png` … `done`. Eyeball `hevy--1440.png`, `hevy--390.png`, `hevy-data--1440.png`, `hevy-data--390.png`: the CONNECT HEVY button is now a legible flat burnt-orange fill with white label, no longer reading as disabled (resolves findings 247, 253).

- [ ] **Step 6: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Fix CONNECT HEVY button fill and label contrast (P1)" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4 (Group A-3): Dead `dark:` variant sweep — remove `dark:*` tokens from all 19 files

Resolves all 28 static P2 `dark:` findings (doc lines 133-158) plus the off-token day-badge/green-outcome instances that resurface on screens (setup-wizard-3 217, setup-wizard-4 green icon 225, programs green pill 235, exercises colour map 241). Per the contract: "a `dark:` Tailwind variant is always dead code — remove it on sight."

**Removal approach (mechanical, per file):** For every `className` string, delete only the `dark:*` class tokens (each `dark:` prefix and the class attached to it, e.g. `dark:bg-zinc-900`, `dark:text-blue-400`). **Never remove the light half** — leave the non-`dark:` class untouched (e.g. `bg-white dark:bg-zinc-900` → `bg-white`; `text-blue-700 dark:text-blue-400` → `text-blue-700`). Collapse any resulting double spaces inside the class string. Do not retint or re-token in this task — that is deliberately deferred to the badge/modal/outcome primitive tasks (A-4/A-5/A-6) and Group B, so this sweep stays purely subtractive and low-risk.

**Files (all 19, with hit counts from the audit; the BlockSequenceEditor comment-only hit and prose lines are NOT touched):**
- Modify: `src/A2S.Web/src/features/exercises/ExerciseLibraryComponents.tsx` (20 hits)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSubstitutionModal.tsx` (15)
- Modify: `src/A2S.Web/src/features/workout/PulledSubstitutionsModal.tsx` (14)
- Modify: `src/A2S.Web/src/features/workout/ExerciseCard.tsx` (14)
- Modify: `src/A2S.Web/src/features/workout/WeightDiscrepancyModal.tsx` (11)
- Modify: `src/A2S.Web/src/features/workout/MissingExercisesModal.tsx` (10)
- Modify: `src/A2S.Web/src/features/workout/CompletionSummary.tsx` (10)
- Modify: `src/A2S.Web/src/features/workout/EditExercisesModal.tsx` (6)
- Modify: `src/A2S.Web/src/features/workout/EditExerciseConfigModal.tsx` (5)
- Modify: `src/A2S.Web/src/components/shared/UndoConfirmationModal.tsx` (5)
- Modify: `src/A2S.Web/src/features/workout/WorkoutHeader.tsx` (3)
- Modify: `src/A2S.Web/src/features/workout/WeightConfirmationModal.tsx` (3)
- Modify: `src/A2S.Web/src/features/workout/SessionRecoveryModal.tsx` (2)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/SelectedExerciseCard.tsx` (2)
- Modify: `src/A2S.Web/src/features/workout/BlockSequenceEditor.tsx` (1 live hit at line 121; the line-25 comment hit is prose — do NOT touch)
- Modify: `src/A2S.Web/src/features/workout/WorkoutDashboard.tsx` (1)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/SimpleDayColumnsView.tsx` (1)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/DayColumnsView.tsx` (1)
- Modify: `src/A2S.Web/src/features/workout/DayCard.tsx` (1)

**Interfaces:**
- Produces: zero live `dark:` occurrences in `src/**/*.tsx` (the one remaining match is the `BlockSequenceEditor.tsx:25` doc-comment prose).

- [ ] **Step 1: Enumerate every live hit**

```bash
grep -rn "dark:" src/A2S.Web/src --include="*.tsx"
```
Expected: 126 hits across the 19 files above (plus the 1 prose comment line). Work file-by-file.

- [ ] **Step 2: In each of the 19 files, delete the `dark:*` tokens only.** For each matched `className`, remove every `dark:<class>` token, keep the light half, and collapse doubled spaces. Example transformations (from the audit's representative hits):
  - `UndoConfirmationModal.tsx:46` `bg-white dark:bg-zinc-900` → `bg-white`
  - `MissingExercisesModal.tsx:47` `bg-blue-100 dark:bg-blue-900` → `bg-blue-100`
  - `MissingExercisesModal.tsx:79/90` `bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200` → `bg-white hover:bg-zinc-100 border-zinc-300 text-zinc-700`
  - `ExerciseCard.tsx:73` `bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400` → `bg-blue-100 text-blue-700`
  - `DayColumnsView.tsx:76` `bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400` → `bg-blue-100 text-blue-700`
  - `CompletionSummary.tsx:113` `text-green-600 bg-green-100 dark:bg-green-900/30` → `text-green-600 bg-green-100`
  - `ExerciseLibraryComponents.tsx:57-76` each `text-{colour}-600 dark:text-{colour}-400` → `text-{colour}-600`

  Do NOT touch `BlockSequenceEditor.tsx:25` (prose in a doc comment).

- [ ] **Step 3: Confirm only the prose comment remains**

```bash
grep -rn "dark:" src/A2S.Web/src --include="*.tsx"
```
Expected: exactly one match — `BlockSequenceEditor.tsx:25` (the doc-comment line). If any live `className` hit remains, remove it.

- [ ] **Step 4: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 246/249 (baseline).

- [ ] **Step 5: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Remove dead dark: variant tokens across 19 files" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5 (Group A-4): Shared `ReviewModal`/`DecisionListModal` primitive + footer-stacking P1 fix

Resolves the four near-identical review modals (Structure obs), and — via a footer that stacks at ≤390px — the two footer-overflow P1s: modal-progression 174/259 and modal-substitution 180/265. Collapses the duplicated `dark:` clusters (already stripped in Task 4) into one primitive.

**Files:**
- Create: `src/A2S.Web/src/components/shared/ReviewModal.tsx`
- Modify: `src/A2S.Web/src/features/workout/ExerciseProgressionModal.tsx` (footer only — stack buttons at 390px)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSubstitutionModal.tsx` (footer only — stack buttons at 390px)

**Interfaces:**
- Produces: `ReviewModal` — a reusable review/decision modal shell (tinted header, scrollable body, footer that stacks on narrow viewports) that the four decision modals can migrate to. The P1 footer-overflow fix is applied directly to the two overflowing modals' footers so the P1s are retired in this task regardless of migration timing.

**Call sites to migrate to `ReviewModal` (body/decision-list only; each keeps its own copy + accent):**
- `src/A2S.Web/src/features/workout/MissingExercisesModal.tsx`
- `src/A2S.Web/src/features/workout/PulledSubstitutionsModal.tsx`
- `src/A2S.Web/src/features/workout/WeightDiscrepancyModal.tsx`
- `src/A2S.Web/src/features/workout/WeightConfirmationModal.tsx`

- [ ] **Step 1: Create `src/A2S.Web/src/components/shared/ReviewModal.tsx` with exactly this content:**

```tsx
import * as React from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export interface ReviewModalAction {
  label: string;
  onClick: () => void;
  variant?: React.ComponentProps<typeof Button>["variant"];
  disabled?: boolean;
}

interface ReviewModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: React.ReactNode;
  icon?: React.ReactNode;
  /** Optional accent tint for the header, using token utilities only. */
  headerClassName?: string;
  children: React.ReactNode;
  /** Footer actions. Rendered stacked at <=390px, side-by-side above it. */
  actions: ReviewModalAction[];
}

/**
 * Shared review/decision modal shell: tinted header, scrollable token-surfaced
 * body, and a footer that stacks vertically on narrow viewports so action
 * buttons never overflow at 390px.
 */
export function ReviewModal({
  open,
  onOpenChange,
  title,
  description,
  icon,
  headerClassName,
  children,
  actions,
}: ReviewModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg max-h-[80vh] overflow-hidden flex flex-col p-0">
        <DialogHeader className={cn("p-4 border-b", headerClassName)}>
          <div className="flex items-center gap-2">
            {icon}
            <DialogTitle>{title}</DialogTitle>
          </div>
          {description && (
            <DialogDescription className="text-sm mt-1">{description}</DialogDescription>
          )}
        </DialogHeader>

        <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-card">{children}</div>

        <div className="p-4 border-t border-border flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          {actions.map((action) => (
            <Button
              key={action.label}
              variant={action.variant}
              onClick={action.onClick}
              disabled={action.disabled}
              className="w-full sm:w-auto"
            >
              {action.label}
            </Button>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}
```

Note: the footer uses `flex flex-col-reverse gap-2 sm:flex-row sm:justify-end` — buttons stack full-width below the `sm` breakpoint (640px, which includes 390px) and sit side-by-side, right-aligned, at ≥`sm`. `flex-col-reverse` keeps the primary action visually last/bottom-most on mobile. `w-full sm:w-auto` on each button prevents horizontal overflow.

- [ ] **Step 2: Fix the modal-progression footer overflow (P1, finding 174/259)** in `ExerciseProgressionModal.tsx`. The current footer (lines 152-172) is `<div className="p-4 border-t flex justify-between items-center">` holding "Change Progression Type" (left) and "Close" (right). Replace the footer container className with one that stacks at ≤390px:

```tsx
        {/* Footer */}
        <div className="p-4 border-t flex flex-col-reverse gap-2 sm:flex-row sm:justify-between sm:items-center">
```
and add `className="w-full sm:w-auto"` to each of the two footer `<Button>`s (the "Change Progression Type" outline button and the "Close" outline button), replacing/merging with their existing `className` where present. Do not change onClick, variant, or the SVG.

- [ ] **Step 3: Fix the modal-substitution footer overflow (P1, finding 180/265)** in `ExerciseSubstitutionModal.tsx`. The current footer (lines 354-365) is `<div className="p-4 border-t flex justify-end gap-3">` with "Cancel" (outline) and "Substitute Exercise" (default). Replace with:

```tsx
        {/* Actions */}
        <div className="p-4 border-t flex flex-col-reverse gap-2 sm:flex-row sm:justify-end sm:gap-3">
          <Button variant="outline" onClick={handleClose} className="w-full sm:w-auto">
            Cancel
          </Button>
          <Button
            onClick={handleSubstitute}
            disabled={!selectedExercise || !substitutionType}
            className="w-full sm:w-auto"
          >
            Substitute Exercise
          </Button>
        </div>
```

- [ ] **Step 4: Migrate the four decision modals to `ReviewModal`.** For each of `MissingExercisesModal.tsx`, `PulledSubstitutionsModal.tsx`, `WeightDiscrepancyModal.tsx`, `WeightConfirmationModal.tsx`: replace the hand-rolled `<Dialog><DialogContent>…<DialogHeader>…body…footer</DialogContent></Dialog>` scaffold with `<ReviewModal open={true} onOpenChange={…} title=… description=… icon=… headerClassName="bg-blue-100 text-blue-800" actions={[…]}>{decision list body}</ReviewModal>`. Keep each modal's per-row decision-list JSX and its accent tint (the `headerClassName` supplies the colour). Keep the exact same `onApply`/`onComplete`/decision-state handlers — markup swap only, no logic change. Preserve every `data-testid` and `aria-pressed` attribute so existing tests stay green.

- [ ] **Step 5: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 246/249 (baseline). If a migrated-modal test fails on markup structure, adjust the migration to preserve the queried role/testid — do not change the test.

- [ ] **Step 6: Re-screenshot and eyeball** (stack running) — open each modal via the app, or capture the routes that host them:

```bash
node tools/audit-capture.mjs /workout
```
Then open the progression modal (from a workout exercise) and the substitution modal at 390px and confirm both footers stack with no horizontal clipping of "Close"/"Cancel"/"Substitute Exercise" (resolves P1s 174, 180).

- [ ] **Step 7: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Add ReviewModal primitive; stack modal footers at 390px (P1)" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6 (Group A-5): Shared `<Badge>` primitive — fixes "Per Side" pill P1 and triplicated blue day badge

Resolves the "Per Side" pill P1 (workout-session 161), the triplicated blue day-number badge (Structure obs; `DayColumnsView`/`SelectedExerciseCard`/`SimpleDayColumnsView`), and the setup-wizard-3 day badge (217). A single `<Badge>` with a min-width and token colours fixes the collapse and the off-token blue at once.

**Files:**
- Create: `src/A2S.Web/src/components/ui/badge.tsx`
- Modify: `src/A2S.Web/src/features/workout/ExerciseCard.tsx` (the two "Per Side" pills at lines 73 and 106; the "Temp Sub" and "New weight" pills for consistency)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/DayColumnsView.tsx` (day badge, line 76)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/SelectedExerciseCard.tsx` (order badge line 34; day badge line 140)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/SimpleDayColumnsView.tsx` (day badge, line 32)

**Interfaces:**
- Produces: `Badge` and `DayBadge` — token-coloured, min-width badge primitives. `Badge` has a `whitespace-nowrap` label pill (fixes the "Per / Side" two-line collapse); `DayBadge` is the circular numeric day/order badge with a `min-w` so it never collapses to an oval.

- [ ] **Step 1: Create `src/A2S.Web/src/components/ui/badge.tsx` with exactly this content:**

```tsx
import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center justify-center whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium",
  {
    variants: {
      variant: {
        neutral: "bg-muted text-muted-foreground",
        primary: "bg-primary/10 text-primary",
        accent: "bg-accent/15 text-accent-foreground",
        info: "bg-secondary text-secondary-foreground",
        warning: "bg-warning/15 text-warning",
        success: "bg-success/15 text-success",
      },
    },
    defaultVariants: {
      variant: "neutral",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

/** Small token-coloured label pill. `whitespace-nowrap` prevents shape collapse. */
export function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}

/** Circular numeric day/order badge; min-width stops it collapsing to an oval. */
export function DayBadge({
  value,
  className,
}: {
  value: React.ReactNode;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center justify-center min-w-7 h-7 rounded-full bg-primary/10 text-primary text-sm font-bold shrink-0",
        className
      )}
    >
      {value}
    </span>
  );
}
```

Notes: `--color-warning`/`--color-success` tokens exist in `index.css`. `bg-primary/10 text-primary` replaces the off-token `bg-blue-100 text-blue-700` day badge. `whitespace-nowrap` in the base pill fixes the "Per Side" two-line wrap.

- [ ] **Step 2: Migrate the "Per Side" pills in `ExerciseCard.tsx`** — replace both occurrences (collapsed-card line 73 and expanded line 105-108) of the `<span className="text-xs px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 …">Per Side</span>` (the `dark:` half was already stripped in Task 4) with:
```tsx
<Badge variant="info" className="shrink-0">Per Side</Badge>
```
Replace the "Temp Sub" pill (`bg-yellow-100 text-yellow-700`) with `<Badge variant="warning">Temp Sub</Badge>` and the "New weight — match your stack" pill (`bg-amber-100 text-amber-700`) with `<Badge variant="warning">New weight — match your stack</Badge>`. Add `import { Badge } from "@/components/ui/badge";` at the top.

- [ ] **Step 3: Migrate the day/order badges** — in `DayColumnsView.tsx` (line 76) and `SimpleDayColumnsView.tsx` (line 32) replace the `<span className="inline-flex items-center justify-center w-7 h-7 rounded-full bg-blue-100 text-blue-700 text-sm font-bold">{day}</span>` with `<DayBadge value={day} />`. In `SelectedExerciseCard.tsx`, replace the order badge (line 34, `w-5 h-5 … bg-blue-100 text-blue-700`) with `<DayBadge value={exercise.orderInDay} className="min-w-5 w-5 h-5 text-xs" />` and the "Day N" inline badge (line 140, `bg-blue-500/10 text-blue-600`) with `<Badge variant="primary">Day {exercise.assignedDay}</Badge>`. Add `import { Badge, DayBadge } from "@/components/ui/badge";` to each file (import only what each uses).

- [ ] **Step 4: Confirm no `bg-blue-100`/`bg-blue-500/10` day-badge remnants remain in the four migrated files**

```bash
grep -nE "bg-blue-100|bg-blue-500/10" src/A2S.Web/src/features/workout/ExerciseCard.tsx src/A2S.Web/src/features/workout/ExerciseSelectionV2/DayColumnsView.tsx src/A2S.Web/src/features/workout/ExerciseSelectionV2/SelectedExerciseCard.tsx src/A2S.Web/src/features/workout/ExerciseSelectionV2/SimpleDayColumnsView.tsx
```
Expected: no matches.

- [ ] **Step 5: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 246/249 (baseline).

- [ ] **Step 6: Re-screenshot and eyeball** (stack running):

```bash
node tools/audit-capture.mjs /workout /setup
```
Open `/workout` session at 390px and confirm the "Per Side" badge is a single-line pill of fixed shape next to the exercise title (resolves P1 161); confirm the setup-wizard day badges are token-orange circles, not off-token blue (resolves 217).

- [ ] **Step 7: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Add Badge/DayBadge primitive; fix Per Side pill collapse (P1) and blue day badges" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 7 (Group A-6): Shared `outcomeToStatus()` classifier + token status badge

Resolves the fragile `.includes()` outcome-colour logic in `CompletionSummary` + `SimulationPage` (Structure obs) and the off-token green/status colours they emit.

**Files:**
- Create: `src/A2S.Web/src/lib/outcomeStatus.ts`
- Create: `src/A2S.Web/src/lib/outcomeStatus.test.ts`
- Modify: `src/A2S.Web/src/features/workout/CompletionSummary.tsx` (replace `getOutcomeStyle`/`getOutcomeLabel`, lines 111-135; call sites at 187, 193, 241-242)
- Modify: `src/A2S.Web/src/features/workout/SimulationPage.tsx` (replace the outcome-string colour switch, lines 337-348)

**Interfaces:**
- Produces: `outcomeToStatus(change: string): OutcomeStatus` and a `statusBadgeClass(status)` returning token utilities. Both files consume it instead of hand-rolled `.includes()` + non-token colours.

- [ ] **Step 1: Create `src/A2S.Web/src/lib/outcomeStatus.ts` with exactly this content:**

```ts
export type OutcomeStatus = "success" | "failed" | "deload" | "maintained";

/**
 * Classify a free-text progression change string into a status.
 * Extracted from CompletionSummary/SimulationPage to remove duplicated,
 * order-dependent `.includes()` chains and off-token colours.
 */
export function outcomeToStatus(change: string): OutcomeStatus {
  const c = change.toLowerCase();
  if (c.includes("increased") || c.includes("added")) return "success";
  if (c.includes("decreased") || c.includes("reduced")) return "failed";
  if (c.includes("deload")) return "deload";
  return "maintained";
}

const STATUS_LABEL: Record<OutcomeStatus, string> = {
  success: "SUCCESS",
  failed: "FAILED",
  deload: "DELOAD",
  maintained: "MAINTAINED",
};

export function outcomeLabel(status: OutcomeStatus): string {
  return STATUS_LABEL[status];
}

/** Token-based badge classes for an outcome status (fill + foreground). */
export function statusBadgeClass(status: OutcomeStatus): string {
  switch (status) {
    case "success":
      return "text-success bg-success/15";
    case "failed":
      return "text-destructive bg-destructive/15";
    case "deload":
      return "text-primary bg-primary/10";
    case "maintained":
      return "text-warning bg-warning/15";
  }
}

/** Token foreground-only class for a raw simulation outcome value. */
export function simOutcomeClass(outcome: string): string {
  switch (outcome) {
    case "Success":
      return "text-success";
    case "Fail":
      return "text-destructive";
    default:
      return "text-warning";
  }
}
```

- [ ] **Step 2: Create `src/A2S.Web/src/lib/outcomeStatus.test.ts` with exactly this content:**

```ts
import { describe, it, expect } from "vitest";
import {
  outcomeToStatus,
  outcomeLabel,
  statusBadgeClass,
  simOutcomeClass,
} from "./outcomeStatus";

describe("outcomeToStatus", () => {
  it("classifies increased/added as success", () => {
    expect(outcomeToStatus("Weight increased to 105kg")).toBe("success");
    expect(outcomeToStatus("Added a set")).toBe("success");
  });
  it("classifies decreased/reduced as failed", () => {
    expect(outcomeToStatus("Weight decreased")).toBe("failed");
    expect(outcomeToStatus("Sets reduced")).toBe("failed");
  });
  it("classifies deload", () => {
    expect(outcomeToStatus("Deload week applied")).toBe("deload");
  });
  it("defaults to maintained", () => {
    expect(outcomeToStatus("Maintained current weight")).toBe("maintained");
    expect(outcomeToStatus("no change")).toBe("maintained");
  });
});

describe("outcomeLabel", () => {
  it("maps status to upper-case label", () => {
    expect(outcomeLabel("success")).toBe("SUCCESS");
    expect(outcomeLabel("failed")).toBe("FAILED");
    expect(outcomeLabel("deload")).toBe("DELOAD");
    expect(outcomeLabel("maintained")).toBe("MAINTAINED");
  });
});

describe("statusBadgeClass", () => {
  it("returns token-only classes (no raw colour literals)", () => {
    for (const s of ["success", "failed", "deload", "maintained"] as const) {
      const cls = statusBadgeClass(s);
      expect(cls).not.toMatch(/#|rgb\(|hsl\(|dark:/);
    }
    expect(statusBadgeClass("success")).toContain("text-success");
    expect(statusBadgeClass("failed")).toContain("text-destructive");
  });
});

describe("simOutcomeClass", () => {
  it("maps sim outcomes to token foregrounds", () => {
    expect(simOutcomeClass("Success")).toBe("text-success");
    expect(simOutcomeClass("Fail")).toBe("text-destructive");
    expect(simOutcomeClass("Maintain")).toBe("text-warning");
  });
});
```

- [ ] **Step 3: Rewire `CompletionSummary.tsx`** — delete the local `getOutcomeStyle` (lines 111-122) and `getOutcomeLabel` (lines 124-135). Add `import { outcomeToStatus, outcomeLabel, statusBadgeClass } from "@/lib/outcomeStatus";`. At the call sites:
  - line 187: `className={\`p-3 rounded-lg ${getOutcomeStyle(change.change)}\`}` → `className={\`p-3 rounded-lg ${statusBadgeClass(outcomeToStatus(change.change))}\`}`
  - line 193: `{getOutcomeLabel(change.change)}` → `{outcomeLabel(outcomeToStatus(change.change))}`
  - lines 241-242: replace the `getOutcomeLabel(change.change) === "SUCCESS" ? "text-green-600" : … === "FAILED" ? "text-red-600"` chain with `statusBadgeClass(outcomeToStatus(change.change))` (or, if only a foreground is needed there, keep the label compare but swap the literals to `text-success`/`text-destructive`/`text-warning` tokens). No behavior change to which label/colour maps to which outcome.

- [ ] **Step 4: Rewire `SimulationPage.tsx`** — replace the inline outcome colour ternary (lines 337-348) `className={ e.outcome === 'Success' ? 'text-green-600' : e.outcome === 'Fail' ? 'text-destructive' : 'text-yellow-600' }` with `className={simOutcomeClass(e.outcome)}`, and change the deload `<span className="text-blue-500">deload</span>` (line 348) to `<span className="text-primary">deload</span>`. Add `import { simOutcomeClass } from "@/lib/outcomeStatus";`.

- [ ] **Step 5: Run the new unit test, then full build/test**

```bash
cd src/A2S.Web && npx vitest run src/lib/outcomeStatus.test.ts
```
Expected: 4 describe blocks pass. Then:
```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; test count is now 250/253 (247→250 passing after +4 new tests minus consolidation; the 3 `ExerciseLibraryPage.test.tsx` failures unchanged). Confirm no new failures.

- [ ] **Step 6: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Extract shared outcomeToStatus classifier with token status colours" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 8 (Group A-7): Typography-scale application + spacing rhythm

Resolves the residual nav/heading display-font findings beyond the button fix (cross-screen 140 nav, dashboard headings 147) and normalises `p-4`/`p-6` rhythm. Lowest-leverage Group A item.

**Files:**
- Modify: `src/A2S.Web/src/components/layout/Navbar.tsx` (remove ALL-CAPS/display-font treatment on brand + links + "PLAYER"; findings 140/168/225)
- Modify: `src/A2S.Web/src/features/auth/DashboardPage.tsx` (apply `.text-hero`/`h2` scale to hero + section headings; finding 147)

**Interfaces:**
- Produces: nav and dashboard headings render in the system font at the token type scale, no `uppercase`/`tracking-wide`/`font-display`-forced-caps on nav links.

- [ ] **Step 1: Read `Navbar.tsx` and locate the caps/letterspacing** — the audit names `Navbar.tsx:60,73,108,145,160` for the ALL-CAPS brand/link/PLAYER treatment. For each, remove `uppercase` and `tracking-*` classes and any decorative `font-[…]`/`font-display` that forces the retro look; keep the link layout/spacing classes. Nav links should read as normal-case system-font text (`text-sm font-medium text-muted-foreground` for links, active state via `text-foreground`/token accent — do not introduce a new colour).

```bash
grep -nE "uppercase|tracking-wide|tracking-\[|font-\[" src/A2S.Web/src/components/layout/Navbar.tsx
```
Expected after edit: no matches.

- [ ] **Step 2: Apply the type scale in `DashboardPage.tsx`** — the hero "Welcome back, …" heading should use `.text-hero` (one per page); section headings ("Quick Stats", "Current Program", "This Week's Training", "Exercise Progression", "Personal Records") should use `h2`/`h3` element styles or `text-xl`/`text-2xl font-semibold` per the contract. Remove any `uppercase`/`tracking-*`/decorative-font classes on these headings. Normalise card padding on this page to `p-6` (or `p-4` for compact lists) where an off-rhythm value (not 4/6/8) appears.

```bash
grep -nE "uppercase|tracking-wide|font-\[" src/A2S.Web/src/features/auth/DashboardPage.tsx
```
Expected after edit: no matches.

- [ ] **Step 3: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 250/253 (baseline carried from Task 7).

- [ ] **Step 4: Re-screenshot and eyeball** (stack running):

```bash
node tools/audit-capture.mjs /dashboard
```
Confirm nav links and dashboard headings render in the system font, normal case (resolves 140, 147). Open the mobile menu at 390px and confirm menu items are normal-case (resolves 168).

- [ ] **Step 5: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Apply system-font type scale to nav and dashboard headings" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Group B — screen-by-screen residuals (worst-first)

Findings already resolved by Group A are excluded (noted per screen). Each task's verification is: `npm run build` + `npm test` + re-run `tools/audit-capture.mjs` for that route + eyeball against the original finding.

---

### Task 9 (Group B-1): setup-wizard-3-exercises residuals

Findings addressed (quoted from doc):
- 215 (P1): *"the day columns collapse to a 2-up grid of very narrow cards where each exercise row loses its NAME entirely: rows show only the index badge, weight, reps and sets … with no exercise title"*
- 216 (P2): *"The step indicator's 'Confirm' node is clipped off the right edge, same overflow as step 2."*
- 218 (P2): *"The per-exercise edit (pencil) and delete (trash) icon buttons on each program row are ~20-24px targets … under the 44px minimum"*
- 220 (P3): *"the active 'All' chip and every 'Add' button are the same solid orange, so the selected-filter state is not distinguishable from the always-orange Add actions"*

(Day-badge 217 and BACK/NEXT 219 resolved by Group A.)

**Files:**
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/SelectedExerciseCard.tsx` (P1 name-squeeze 215; touch targets 218)
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/SimpleDayColumnsView.tsx` + `DayColumnsView.tsx` (2-up grid → single column at 390px, 215)
- Modify: the step-indicator component used by `SetupWizard.tsx` (216) — locate in Step 2
- Modify: the equipment-filter chip / Add-button rendering (220) — locate in Step 4

- [ ] **Step 1: Fix the exercise-name squeeze (P1 215)** — in `SelectedExerciseCard.tsx` the name is `<h4 className="font-medium text-sm flex-1 min-w-0 truncate">` (line 59). At 390px inside a 2-up grid the flex row leaves it no room. Two coordinated changes:
  (a) In `SimpleDayColumnsView.tsx` (line 116) and `DayColumnsView.tsx` (line 251) change the grid `grid grid-cols-2 lg:grid-cols-3 gap-4` → `grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4` so mobile is single-column (each card gets full width).
  (b) In `SelectedExerciseCard.tsx`, keep `truncate` but ensure the name row does not drop the title: verify the `<h4>` keeps `flex-1 min-w-0` (it does) so with the wider single-column card the name renders. The name being *present but truncated* is acceptable; the P1 is that it vanished entirely — single-column restores it.

- [ ] **Step 2: Fix the step-indicator overflow (P2 216)** — locate the wizard step indicator:
```bash
grep -rniE "step.?indicator|Start.*Template.*Exercises.*Confirm|stepper" src/A2S.Web/src/features/workout --include="*.tsx" -l
```
Read the matched component. The 4-node rail overflows at 390px. Make the rail horizontally scrollable/compact: wrap the node row in `<div className="overflow-x-auto">` and/or reduce per-node label size and connector width at the mobile breakpoint so the final "Confirm" node is not clipped. Do not change step logic.

- [ ] **Step 3: Fix touch targets (P2 218)** — in `SelectedExerciseCard.tsx` the edit/remove buttons are `className="p-1 rounded-md …"` (lines 66, 85) with `w-3.5 h-3.5` icons (~24px total). Increase the tappable area: change `p-1` → `p-2.5` and add `min-w-11 min-h-11 inline-flex items-center justify-center` so each button meets the 44px minimum. Keep the icon size and aria-labels.

- [ ] **Step 4: Fix the filter-chip vs Add-button colour collision (P3 220)** — locate the equipment filter chips and Add buttons:
```bash
grep -rniE "All \(|equipment.*filter|onAdd|Add<" src/A2S.Web/src/features/workout/ExerciseSelectionV2 --include="*.tsx" -l
```
Give the active filter chip a distinct token treatment from the Add action: active chip = `bg-primary/15 text-primary border border-primary` (outline/tinted), Add button = solid `bg-primary text-primary-foreground` (the default Button). This differentiates selected-state from action. Token utilities only.

- [ ] **Step 5: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 250/253 (baseline).

- [ ] **Step 6: Re-screenshot and eyeball**

```bash
node tools/audit-capture.mjs /setup
```
At 390px on the Exercises step: confirm exercise names render (215 resolved), the "Confirm" step node is visible/not clipped (216), edit/delete buttons are ≥44px (218), and the active filter chip is visually distinct from Add buttons (220). Eyeball against `setup-wizard-3-exercises--390.png` original.

- [ ] **Step 7: Commit**

```bash
git add src/A2S.Web/src
git commit -m "setup-wizard-3: fix mobile name squeeze (P1), step overflow, touch targets, chip colour" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 10 (Group B-2): workout-session residuals

Findings addressed (quoted):
- 162 (P2): *"The per-exercise edit (pencil) and swap (arrows) icon buttons are ~24px targets, under the 44px minimum"*
- 163 (P2): *"The floating island/palm avatar overlaps the swap icon on the Crucifix Tricep Pulldown card (390px) and floats mid-card on desktop, obscuring an interactive control."*
- 164 (P3): *"LOG (outline variant) buttons show a thin red vertical artifact at the right of the label on desktop … a rendering/caret artifact from the outline border"*

(Per-Side badge 161 resolved by Task 6; button 160 by Tasks 2/3.)

**Files:**
- Modify: `src/A2S.Web/src/features/workout/ExerciseCard.tsx` (edit/swap buttons 162; LOG artifact 164)
- Modify: the floating palm/avatar widget component (163) — locate in Step 2

- [ ] **Step 1: Fix edit/swap touch targets (P2 162)** — in `ExerciseCard.tsx` the two header `<Button variant="ghost" size="sm">` (lines 127-148) hold `w-4 h-4` SVGs. `size="sm"` is `h-10` (40px), just under 44. Change both to `size="icon"` (which is `h-12 w-12`) or add `className="min-w-11 min-h-11"` merged with the existing `text-muted-foreground hover:text-foreground`. Keep aria-labels and onClick.

- [ ] **Step 2: Fix the palm-avatar overlap (P2 163)** — locate the fixed decorative avatar widget:
```bash
grep -rniE "palm|island|avatar|fixed bottom" src/A2S.Web/src --include="*.tsx" -l
```
Read the widget. It is a `fixed`-positioned decorative overlay sitting over interactive controls. Per the contract ("no glows/decorative elements over real data") the lowest-risk behavior-preserving fix is to add `pointer-events-none` so it never intercepts taps, and give it a lower `z-index` and/or a safe-area offset (`bottom-4 right-4` reduced, or hidden below `sm`) so it does not visually cover the swap icon. Since the same widget causes overlaps on dashboard/sign-in/sign-up/settings/setup-wizard (findings 148, 197, 203, 211, 227, 260, 288, 296, 312, 345), fixing it once here resolves all of them — note this in the commit. Do not remove the widget (visual-identity element); reposition/`pointer-events-none` only.

- [ ] **Step 3: Fix the LOG red vertical artifact (P3 164)** — in `ExerciseCard.tsx` the LOG button (lines 247-255) uses `variant="outline"`. After Task 2 the outline variant is `border border-border` (no red). Re-screenshot in Step 5 to confirm the artifact is gone; if a stray artifact persists it comes from an adjacent element's border — inspect and remove any `border-r`/`border-destructive` on the set-row container. Token-only.

- [ ] **Step 4: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 250/253 (baseline).

- [ ] **Step 5: Re-screenshot and eyeball**

```bash
node tools/audit-capture.mjs /workout
```
Open the session at 390px + 1440px: confirm edit/swap buttons ≥44px (162), the palm widget no longer covers the swap icon and does not intercept taps (163), and no red vertical artifact on LOG buttons (164).

- [ ] **Step 6: Commit**

```bash
git add src/A2S.Web/src
git commit -m "workout-session: touch targets, palm-widget pointer-events, LOG artifact" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 11 (Group B-3/B-4): modal-progression + modal-substitution polish residuals (P3)

Findings addressed (quoted):
- 176 (P3, modal-progression): *"The 'Reps Per Set' tag is orange and 'Day 1' tag grey — two differently-styled tags on one header row is a minor inconsistency."* (empty-state sparseness noted, positive-leaning)
- 182 (P3, modal-substitution): *"The search input's orange focus ring is heavy but on-brand."* (positive-leaning)

**Files:**
- Modify: `src/A2S.Web/src/features/workout/ExerciseProgressionModal.tsx` (header tags, lines 72-79)

- [ ] **Step 1: Unify the two header tags (176)** — in `ExerciseProgressionModal.tsx` the header (lines 73-78) renders the progression-label tag as `bg-primary/10 text-primary` and the "Day N" tag as `bg-muted text-muted-foreground`. Migrate both to the shared `Badge` from Task 6 for one consistent tag style: `<Badge variant="primary">{progressionLabel}</Badge>` and `<Badge variant="neutral">Day {exercise.assignedDay}</Badge>`. Add `import { Badge } from "@/components/ui/badge";`. This keeps two variants but one primitive/shape, resolving the "differently-styled tags" nit.

- [ ] **Step 2: Finding 182 is positive-leaning — no code change.** The heavy-but-on-brand orange focus ring is explicitly "on-brand" in the finding; record as deferred-no-op in the resolution log (Task 15). No edit.

- [ ] **Step 3: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 250/253 (baseline).

- [ ] **Step 4: Re-screenshot and eyeball**

```bash
node tools/audit-capture.mjs /workout
```
Open the progression modal; confirm the two header tags share one badge shape (176 resolved).

- [ ] **Step 5: Commit**

```bash
git add src/A2S.Web/src
git commit -m "modal-progression: unify header tags via Badge primitive" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 12 (Group B-5): sign-up full-page theme break (P1) + sign-in/sign-up Clerk integration

Findings addressed (quoted):
- 195 (P1, sign-up): *"The ENTIRE page background is light/near-white … sign-up abandons the Arcade Minimal dark theme entirely — a full-page theme break, not just the embedded widget."*
- 196 (P2, sign-up) / 188 (P2, sign-in): *"the Clerk card uses Clerk's default light appearance and a near-black 'Continue' button rather than the app's burnt-orange primary"*
- 189 (P2, sign-in): non-primary Continue button.
- 190 (P3, sign-in): empty-band framing; 191/197 (P3): palm overlap (resolved by Task 10 Step 2).

**Files:**
- Modify: the sign-up page shell (P1 195) — locate in Step 1
- Modify: sign-in + sign-up Clerk `appearance` props (188/189/196) — locate in Step 2

- [ ] **Step 1: Fix the full-page theme break (P1 195)** — locate the sign-up page:
```bash
grep -rniE "SignUp|sign-up" src/A2S.Web/src --include="*.tsx" -l
```
Read the sign-up page component and compare its shell wrapper to the sign-in page's. Sign-in renders the correct near-black background; sign-up does not. Apply the same dark shell wrapper (`bg-background text-foreground min-h-screen` on the outer container, matching sign-in exactly). The defect is a missing/overridden background class on the sign-up shell — align it to sign-in.

- [ ] **Step 2: Theme the Clerk cards (188/189/196)** — locate the `<SignIn>` / `<SignUp>` Clerk component usages. Pass an `appearance` prop themed to the app: set the card surface to the app's dark card (`variables: { colorBackground, colorText, colorPrimary }` sourced from the tokens via `var(--color-*)`, and `elements` overrides for the primary/`formButtonPrimary` to `bg-primary text-primary-foreground`). Concretely:
```tsx
appearance={{
  variables: {
    colorPrimary: 'hsl(25 80% 45%)',
    colorBackground: 'hsl(240 10% 10%)',
    colorText: 'hsl(0 0% 95%)',
    colorInputBackground: 'hsl(0 0% 20%)',
    colorInputText: 'hsl(0 0% 95%)',
  },
  elements: {
    card: 'bg-card border border-border',
    formButtonPrimary: 'bg-primary text-primary-foreground hover:bg-primary/90',
  },
}}
```
(Clerk's `appearance.variables` requires concrete colour strings — this is the one sanctioned place outside `index.css`/`lib` where literal `hsl()` values are unavoidable because Clerk renders in its own shadow context and cannot read Tailwind utilities. Mirror the token values from `index.css` exactly.) Apply the identical `appearance` to both sign-in and sign-up.

- [ ] **Step 3: Note the empty-band framing (P3 190)** — optional polish: constrain the auth layout vertical centring so the card is not floating in a tall void. If cheap, add `justify-center` / a max-height wrapper; otherwise record 190 as deferred-P3 in the resolution log.

- [ ] **Step 4: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 250/253 (baseline).

- [ ] **Step 5: Re-screenshot and eyeball** (use the tool's default auth — it captures via storage state; for logged-out auth screens capture manually or add `/sign-in /sign-up` and confirm they render logged-out):

```bash
node tools/audit-capture.mjs /sign-in /sign-up
```
Confirm sign-up now has the near-black dark background matching sign-in (195 resolved), and both Clerk cards render on dark surfaces with a burnt-orange Continue button (188/189/196 resolved).

- [ ] **Step 6: Commit**

```bash
git add src/A2S.Web/src
git commit -m "sign-up: fix full-page theme break (P1); theme Clerk cards to Arcade Minimal" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 13 (Group B-6 … B-20): remaining lighter screen residuals

Each finding below is addressed by referencing the finding + file (Group A already resolved the heavy items on these screens). Batch-commit in logical groups.

**sign-in (B-6):** 188/189 done in Task 12; 190 empty-band (P3, deferred or cheap centring per Task 12 Step 3); 191 palm overlap (resolved Task 10 Step 2). No residual code beyond Task 12.

**setup-wizard-4-confirm (B-7):**
- 224 (P2): *"The step indicator's final 'Confirm' node/label is clipped at the right edge, same 4-node overflow as steps 2 and 3."* — resolved by the step-indicator scroll fix in Task 9 Step 2 (same shared component). Verify only.
- 227 (P3): edge-gutter + palm overlap — palm resolved Task 10; add `pr-4`/gutter on the footer button container if it touches the edge at 390px. File: the confirm-step markup in `SetupWizard.tsx`.
- 225 (green icon) resolved by Task 4/Task 7 tokens; 226 (CREATE PROGRAM) by Tasks 2/3.

**setup-wizard-2-template (B-8):**
- 208 (P2): step-indicator overflow — resolved by Task 9 Step 2. Verify only.
- 210 (P3): *"no selected/checked affordance on template cards"* — add a token selected-state ring on the chosen template card (`ring-2 ring-primary`) driven by the existing `selectedTemplate` state in `SetupWizard.tsx`. Token only, no logic change.
- 211 (P3) palm overlap — resolved Task 10.

**setup-wizard-1-welcome (B-9):**
- 201 (P2): *"The step indicator shows only THREE nodes here … but every subsequent step shows FOUR"* — make the rail fixed-length: render all four nodes (Start · Template · Exercises · Confirm) on the welcome step too, greying the not-yet-reached ones, in the step-indicator component. If the welcome step legitimately precedes the template/scratch choice, render the four-node rail with the "Template" node in a pending style rather than omitting it. File: step-indicator component + `SetupWizard.tsx` `getSteps`. Presentation only — do not change `getSteps` logic that drives navigation; only the indicator's displayed node set.
- 203 (P3) palm overlap — resolved Task 10.

**dashboard (B-10):**
- 148 (P2) palm overlap — resolved Task 10.
- 149 (P3): *"~20 near-identical cards each reading 'Not enough data yet' … a very long, low-information wall"* — collapse the per-exercise empty-state into a single aggregated empty state when all exercises lack data. File: `DashboardPage.tsx`. Behavior-preserving presentation change (render one card instead of N when data is uniformly empty).
- 150 (P3) positive note (PR empty-state is correct) — no change.

**workout (B-11):**
- 154 (meta chips) by Task 2; 155 (P3): dense day-column separation — add a divider/heavier exercise-name weight between exercises in the day columns. File: the workout day-column component. 156 (P3): blurred next-week-preview scroll length — optionally cap the preview height; low priority, may defer.

**programs (B-12):**
- 234 (buttons) by Task 2.
- 235 (P3): *"Two differently-styled status pills … 'Active' green-tinted (off-token green) … 'Active Program' orange-tinted … redundant"* — migrate both to `Badge` (Task 6) and drop the redundant duplicate so one status pill remains, token-coloured. File: the programs card component.
- 236 (P3): *"Body copy reads 'FourDay-Day Split' — a data/label glitch (double 'Day')"* — fix the label template that concatenates variant + "Day Split". File: locate with `grep -rn "Day Split" src/A2S.Web/src`.

**exercises (B-13):**
- 241 (muscle-group colour map) by Task 4 (and Group C `ExerciseLibraryComponents` token lookup, Task 18).
- 242 (P3): *"444 near-identical tiles … no sticky filter/section affordance"* — make the filter sidebar sticky (`sticky top-N`) so filters stay reachable while scrolling. File: `ExerciseLibraryPage.tsx`. Presentation only.

**nav-mobile (B-14):**
- 168 (menu ALL-CAPS) by Task 8.
- 169 (P3): *"The active item … uses a full-width burnt-orange-tinted highlight bar; the tint is quite dark/muted and … reads more like a hover than a clear 'you are here'"* — strengthen the active state (e.g. `bg-primary/15 text-primary` + a left accent border) so it's distinct from hover. File: `Navbar.tsx` mobile menu. Token only.

**settings (B-15):**
- 259 (button treatment) by Task 2; 260 (palm overlap) by Task 10. Verify only — no residual code.

**hevy (B-16):** 247 by Task 3, 248 by Task 2. No residual.

**hevy-data (B-17):**
- 253 by Task 3.
- 254 (P2): *"The '…' empty state is a single left-aligned muted line … inconsistent with the centred glyph+copy empty states used on Hevy/History/Dashboard; reads as an unstyled placeholder"* — restyle the hevy-data empty state to match the centred glyph + two-line copy pattern used on the Hevy page. File: the hevy-data page component (locate with `grep -rn "view workout data" src/A2S.Web/src`).

**simulate (B-18):** 272 by Task 2; results table/chart deferred (not reached in audit). No residual.

**cross-screen (B-19):** 139-142 all by Tasks 2/3/4. No residual.

**history (B-20):**
- 265 (P3): block legend dots use sanctioned `lib/blockColors.ts` literals — contract sanctions that file; no change (record as sanctioned/deferred).
- 266 (button treatment) by Task 2. No residual.

- [ ] **Step 1: setup-wizard residuals** — apply 210 (template selected-ring), 201 (fixed four-node rail on welcome), 227 (footer gutter). Verify 208/224 resolved by Task 9's step-indicator fix.
- [ ] **Step 2: dashboard/workout residuals** — apply 149 (aggregated empty state), 155 (day-column divider), optionally 156.
- [ ] **Step 3: programs/exercises residuals** — apply 235 (single token status Badge), 236 ("Day Split" label fix), 242 (sticky filters).
- [ ] **Step 4: nav-mobile/hevy-data residuals** — apply 169 (stronger active state), 254 (centred empty state).
- [ ] **Step 5: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 250/253 (baseline).

- [ ] **Step 6: Re-screenshot the touched routes and eyeball each against its finding**

```bash
node tools/audit-capture.mjs /setup /dashboard /workout /programs /exercises /hevy/data
```
Confirm each finding above renders resolved.

- [ ] **Step 7: Commit** (one commit for the batch)

```bash
git add src/A2S.Web/src
git commit -m "Group B screen residuals: wizard affordances, empty states, status badges, sticky filters, active nav" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Group C — structure refactors (behavior-preserving only)

All extract-and-move; no business logic change; existing tests must stay green; no visual diff expected.

---

### Task 14 (Group C-1): Extract `SetupWizard` template conversion into `lib/` with unit tests

Structure obs: *"`SetupWizard.tsx` (639 lines) … the template-conversion logic reads as pure data transformation that could live in `lib/` and be unit-tested independently."*

**Files:**
- Create: `src/A2S.Web/src/lib/templateConversion.ts`
- Create: `src/A2S.Web/src/lib/templateConversion.test.ts`
- Modify: `src/A2S.Web/src/features/workout/SetupWizard.tsx` (lines 31-71 — call the extracted function)

**Interfaces:**
- Produces: `convertTemplateToSelectedExercises(template, exerciseLibrary): SelectedExercise[]` — the pure `template.exercises.map(...)` transform from `applyTemplate` (lines 34-64), taking the library as a parameter. `SetupWizard.applyTemplate` calls it and then does the four `setState` calls (which stay in the component).

- [ ] **Step 1: Create `src/A2S.Web/src/lib/templateConversion.ts`** exporting `convertTemplateToSelectedExercises(template: WorkoutTemplate, exerciseLibrary: ExerciseLibrary): SelectedExercise[]` containing exactly the `template.exercises.map((ex, index) => { … })` block from `SetupWizard.tsx` lines 34-64 (the mapping that builds each `SelectedExercise`, including the `templateData` lookup, `trainingMax`, `isPrimary`, `repRange`, `currentSets`/`targetSets` defaults). Import the types (`WorkoutTemplate`, `SelectedExercise`, `DayNumber`, `ExerciseTemplate`, `WeightUnit`, `ExerciseCategory`) from their existing modules. Keep the null-library guard as: if `!exerciseLibrary` return `[]`.

- [ ] **Step 2: Rewire `SetupWizard.applyTemplate`** (lines 31-71) to:
```tsx
  const applyTemplate = (template: WorkoutTemplate) => {
    if (!exerciseLibrary) return;
    const converted = convertTemplateToSelectedExercises(template, exerciseLibrary);
    setSelectedExercises(converted);
    setWorkoutName(template.name);
    setVariant(template.variant as ProgramVariant);
    setTotalWeeks(template.totalWeeks);
    setBlockSequence(template.blockSequence ?? [1, 2, 3]);
  };
```
Add `import { convertTemplateToSelectedExercises } from "@/lib/templateConversion";`.

- [ ] **Step 3: Create `src/A2S.Web/src/lib/templateConversion.test.ts`** with concrete cases:
```ts
import { describe, it, expect } from "vitest";
import { convertTemplateToSelectedExercises } from "./templateConversion";
import { WeightUnit, ExerciseCategory } from "@/types/workout";

const library = {
  templates: [
    { name: "Squat Barbell", equipment: 0, description: "", defaultSets: 4, defaultRepRange: { minimum: 8, maximum: 12 } },
  ],
} as any;

const template = {
  name: "Test Program",
  variant: 4,
  totalWeeks: 21,
  blockSequence: [1, 2, 3],
  exercises: [
    {
      templateName: "Squat Barbell",
      externalTemplateId: "hevy-123",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 1,
      orderInDay: 1,
      trainingMaxValue: 105,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    {
      templateName: "Unknown Exercise",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 1,
      repRangeMinimum: 6,
      repRangeMaximum: 10,
      startingSets: 3,
      targetSets: 5,
      startingWeight: 45,
    },
  ],
} as any;

describe("convertTemplateToSelectedExercises", () => {
  it("returns [] when library is null", () => {
    expect(convertTemplateToSelectedExercises(template, null as any)).toEqual([]);
  });
  it("maps a known Linear main lift with training max and Primary flag", () => {
    const [ex] = convertTemplateToSelectedExercises(template, library);
    expect(ex.template.name).toBe("Squat Barbell");
    expect(ex.progressionType).toBe("Linear");
    expect(ex.trainingMax).toEqual({ value: 105, unit: WeightUnit.Kilograms });
    expect(ex.isPrimary).toBe(true);
    expect(ex.assignedDay).toBe(1);
    expect(ex.hevyExerciseTemplateId).toBe("hevy-123");
  });
  it("falls back to a stub template for unknown names and preserves rep range/sets", () => {
    const ex = convertTemplateToSelectedExercises(template, library)[1];
    expect(ex.template.name).toBe("Unknown Exercise");
    expect(ex.repRange).toEqual({ minimum: 6, maximum: 10 });
    expect(ex.currentSets).toBe(3);
    expect(ex.targetSets).toBe(5);
    expect(ex.startingWeight).toBe(45);
    expect(ex.isPrimary).toBe(false);
  });
});
```
Adjust field names to match the actual `SelectedExercise`/`WorkoutTemplate` shapes if they differ from the audit-read snapshot; keep the three cases (null library → `[]`; known Linear main lift; unknown-name fallback).

- [ ] **Step 4: Test and build**

```bash
cd src/A2S.Web && npx vitest run src/lib/templateConversion.test.ts && npm run build && npm test
```
Expected: 3 new cases pass; build succeeds; overall 253/256 (baseline + new tests, 3 pre-existing failures unchanged).

- [ ] **Step 5: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Extract template->SelectedExercise conversion to lib with unit tests" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 15 (Group C-2): Extract `EditExercisesModal` Hevy-sync + replace inline confirm modal

Structure obs: *"`EditExercisesModal.tsx` (730 lines) … extract the Hevy delete/recreate/resync orchestration into `useSyncExerciseEditsToHevy`; extract the `ExerciseEditState` derivation; leave the component markup-only. Also replace its bespoke inline 'Remove Exercise' confirm modal (707-727) with the shared confirm primitive."*

**Files:**
- Create: `src/A2S.Web/src/components/shared/ConfirmModal.tsx`
- Create: `src/A2S.Web/src/features/workout/useSyncExerciseEditsToHevy.ts`
- Modify: `src/A2S.Web/src/features/workout/EditExercisesModal.tsx` (extract handleSave Hevy block + ExerciseEditState derivation; swap inline confirm)

**Interfaces:**
- Produces: `ConfirmModal` (shared destructive-confirm dialog); `useSyncExerciseEditsToHevy` hook wrapping the Hevy delete/recreate/resync flow; `EditExercisesModal` reduced toward markup-only and under 500 lines.

- [ ] **Step 1: Create `src/A2S.Web/src/components/shared/ConfirmModal.tsx`** — a small confirm dialog (backdrop + card + title + body + Cancel/Confirm buttons) with props `{ open, onCancel, onConfirm, title, body, confirmLabel, confirmVariant }`. Model the markup on the existing inline confirm (`EditExercisesModal.tsx:707-727`) and `UndoConfirmationModal.tsx`, using token classes (`bg-card border border-border`, `bg-black/70` backdrop) — no `dark:`, no off-token colours. The Confirm button uses `variant={confirmVariant ?? "destructive"}`.

- [ ] **Step 2: Replace the inline confirm in `EditExercisesModal.tsx`** (lines 706-727) with:
```tsx
      <ConfirmModal
        open={exerciseToRemove != null}
        onCancel={() => setExerciseToRemove(null)}
        onConfirm={() => exerciseToRemove && handleRemoveExercise(exerciseToRemove.id, exerciseToRemove.name)}
        title="Remove Exercise"
        body={<>Are you sure you want to permanently remove <strong>{exerciseToRemove?.name}</strong> from this workout?</>}
        confirmLabel="Remove"
      />
```
Add `import { ConfirmModal } from "@/components/shared/ConfirmModal";`.

- [ ] **Step 3: Extract `useSyncExerciseEditsToHevy`** — move the Hevy delete/recreate/get-or-create-folder/refetch/resync orchestration out of `handleSave` (the audit locates it in the `handleSave` block, lines 181-359) into a new hook `useSyncExerciseEditsToHevy` in `src/A2S.Web/src/features/workout/useSyncExerciseEditsToHevy.ts`. The hook exposes an async function that takes the same inputs `handleSave` currently passes to that block and performs the identical calls in the identical order, with the identical toast lifecycle. `handleSave` calls the hook's function where the inline block used to be. **Behavior-preserving: same API calls, same ordering, same 1s wait, same toasts.** Do not change the REST substitution or weight-update logic — extract only the Hevy-sync portion.

- [ ] **Step 4: Extract `ExerciseEditState` derivation** — move the derivation/change-tracking block (audit: lines 80-167) into a co-located helper (a `useMemo`-returning function or a small module) so the component body shrinks. Pure move; identical output.

- [ ] **Step 5: Confirm the file is now under 500 lines**

```bash
wc -l src/A2S.Web/src/features/workout/EditExercisesModal.tsx
```
Expected: < 500. If still over, move more of the extracted derivation/markup helpers out until under the limit.

- [ ] **Step 6: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 253/256 (baseline). Existing EditExercisesModal tests stay green (behavior-preserving).

- [ ] **Step 7: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Extract Hevy sync hook + ConfirmModal from EditExercisesModal (behavior-preserving)" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 16 (Group C-3): Split `ExerciseConfigDialog.tsx` (513 lines) under the limit

Structure obs / over-limit list: *"`ExerciseConfigDialog.tsx` (513 lines) — split marginally-over-limit dialog (contract completeness)."*

**Files:**
- Modify: `src/A2S.Web/src/features/workout/ExerciseSelectionV2/ExerciseConfigDialog.tsx`
- Create: a co-located extracted sub-component/helper file (name per the natural seam found when reading)

- [ ] **Step 1: Read the dialog and find the natural seam** — identify a self-contained sub-form or field group (~50+ lines) that can move to a sibling file (e.g. `ExerciseConfigProgressionFields.tsx`). Extract it, importing back into the dialog. Behavior-preserving; props-passed only.
- [ ] **Step 2: Confirm under 500 lines**

```bash
wc -l src/A2S.Web/src/features/workout/ExerciseSelectionV2/ExerciseConfigDialog.tsx
```
Expected: < 500.
- [ ] **Step 3: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 253/256 (baseline).
- [ ] **Step 4: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Split ExerciseConfigDialog under 500-line limit" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 17 (Group C-4): Split `types/workout.ts` (541 lines) under the limit

Over-limit list (P3): *"`types/workout.ts` — 541 lines, pure type/interface declarations … flagging only for contract completeness."*

**Files:**
- Modify: `src/A2S.Web/src/types/workout.ts`
- Create: a sibling types module (e.g. `src/A2S.Web/src/types/workoutDtos.ts`) re-exported from `workout.ts`

- [ ] **Step 1: Move a cohesive block of DTO/interface declarations** (e.g. the Hevy-sync DTOs or the progression DTOs) into a new sibling file and `export * from "./workoutDtos";` from `workout.ts` so all existing imports (`@/types/workout`) keep resolving unchanged.
- [ ] **Step 2: Confirm both files under 500 and imports unchanged**

```bash
wc -l src/A2S.Web/src/types/workout.ts && grep -rc 'from "@/types/workout"' src/A2S.Web/src --include="*.tsx" --include="*.ts" | head -1
```
Expected: `workout.ts` < 500; import count non-zero and unchanged.
- [ ] **Step 3: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 253/256 (baseline).
- [ ] **Step 4: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Split types/workout.ts under 500-line limit (re-export)" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 18 (Group C-5): Move `MUSCLE_GROUP_CONFIG`/`EQUIPMENT_CONFIG` styling half into a token-driven lookup

Structure obs: *"`ExerciseLibraryComponents.tsx` … `MUSCLE_GROUP_CONFIG`/`EQUIPMENT_CONFIG` … the styling half belongs in a token-driven lookup … rather than one bespoke Tailwind colour pairing per muscle group."* (Relates to A-3; the `dark:` half was stripped in Task 4.)

**Files:**
- Modify: `src/A2S.Web/src/features/exercises/ExerciseLibraryComponents.tsx`
- Create (optional): `src/A2S.Web/src/lib/muscleGroupPalette.ts`

- [ ] **Step 1: Replace the per-muscle bespoke colour classes** (`text-{colour}-600` per group, lines 57-76) with a small token-driven lookup: map each muscle group / equipment type to one of a **fixed categorical palette** derived from `--color-neon-*` tokens (the contract permits `--color-neon-*` for categorical data identity; these badges are categorical labels, akin to chart series). Move the label/icon display data to stay in the component; move the colour mapping to `lib/muscleGroupPalette.ts` returning `var(--color-neon-*)`-based inline styles or a small set of token utility classes. Keep the same visual distinctness (each group still identifiable) but off the raw Tailwind colour ramp.
- [ ] **Step 2: Confirm no raw `text-{colour}-600`/`bg-{colour}-100` map remains**

```bash
grep -nE "text-(red|orange|amber|yellow|green|blue|violet|purple|pink)-[0-9]" src/A2S.Web/src/features/exercises/ExerciseLibraryComponents.tsx
```
Expected: no matches.
- [ ] **Step 3: Build and test**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 253/256 (baseline).
- [ ] **Step 4: Re-screenshot and eyeball**

```bash
node tools/audit-capture.mjs /exercises
```
Confirm muscle/equipment badges are still distinct but token-driven (resolves 241).
- [ ] **Step 5: Commit**

```bash
git add src/A2S.Web/src
git commit -m "Move muscle-group/equipment badge colours to token-driven categorical lookup" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 19 (Group C-6): `SimulationPage.tsx` (694 lines, P3) note — defer split, confirm outcome switch migrated

Over-limit list (P3): internal dev/debug tool, lower priority. Its outcome-string switch was already migrated to `simOutcomeClass` in Task 7.

- [ ] **Step 1: Confirm the outcome switch is already token-based** (done in Task 7):

```bash
grep -nE "text-green-600|text-yellow-600|text-blue-500" src/A2S.Web/src/features/workout/SimulationPage.tsx
```
Expected: no matches (all replaced by `simOutcomeClass`/`text-primary` in Task 7).

- [ ] **Step 2: Record the 694-line split as explicitly deferred** — this is an internal dev tool (P3); splitting it carries no user-facing benefit and is out of the styling-fix critical path. Note the deferral in the resolution log (Task 20). No code change in this task beyond the Step 1 confirmation.

- [ ] **Step 3: No commit** (no code change; deferral recorded in Task 20).

---

### Task 20: Final full re-capture, resolution log, and Known-debt update

**Files:**
- Modify: `docs/superpowers/audits/2026-07-18-frontend-audit-findings.md` (append `## Phase 2 resolution log`)
- Modify: `src/A2S.Web/src/AGENTS.md` (update `## Known debt`)

- [ ] **Step 1: Full re-capture across all routes** (stack running):

```bash
node tools/audit-capture.mjs
```
Expected: every default route captured at 1440 and 390; `done`. Also open the modals (progression, substitution) and `nav-mobile` at 390px and the auth screens logged-out, capturing/eyeballing those manually.

- [ ] **Step 2: Compare against the audit findings** — go through every P1 and P2 in the findings doc and confirm each is resolved (point to the task that fixed it) or explicitly deferred with a reason. The `modal-weight-confirm` screen (not reached in the audit) must be screenshotted now and audited; record any new findings or confirm clean.

- [ ] **Step 3: Append `## Phase 2 resolution log` to the findings doc** with a table: each finding line number → status (`resolved by Task N` / `deferred: reason`). Every P1 (161, 174, 180, 195, 215, 247, 253) marked resolved. Every P2 mapped to its task. P3s batched or marked deferred (182 positive-leaning no-op; 156 optional; 265 sanctioned file; 190 optional centring; SimulationPage/hevyExercises splits deferred). Note the `modal-weight-confirm` audit result.

- [ ] **Step 4: Update `## Known debt` in `src/A2S.Web/src/AGENTS.md`** — replace the placeholder body with the concrete deferred items: SimulationPage 694-line split (internal tool), `hevyExercises.ts`/`workoutTemplates.ts` data-table sizes (out of scope), 60 pre-existing lint errors + `tests/e2e/*` unused-vars + `Navbar.tsx:25` setState-in-effect (toolchain cleanup, scheduled separately), `lib/blockColors.ts` categorical literals (sanctioned), and any P3 deferred above.

- [ ] **Step 5: Full check suite**

```bash
cd src/A2S.Web && npm run build && npm test
```
Expected: build succeeds; 253/256 (or whatever the running total is after all extract-test additions) — the 3 `ExerciseLibraryPage.test.tsx` failures remain the only failures; no new failures; lint error count still 60 (baseline, not increased).

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/audits/2026-07-18-frontend-audit-findings.md src/A2S.Web/src/AGENTS.md
git commit -m "Phase 2 resolution log; update Known debt with deferred items" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Self-review (coverage map)

Every P1 and P2 finding maps to exactly one task (P3s batched or explicitly deferred):

**P1 (7):** 161 → Task 6 · 174 → Task 5 · 180 → Task 5 · 195 → Task 12 · 215 → Task 9 · 247 → Task 3 · 253 → Task 3.

**P2 cross-cutting:** button root-cause (button.tsx:8, cross-screen 139/141/142) → Tasks 2/3 · nav caps 140/168/225 → Task 8 · all 28 static `dark:` (133-158) → Task 4 · CONNECT HEVY fill → Task 3.

**P2 per-screen:** 147 → Task 8 · 148 → Task 10 · 154/155-context → Task 2 + Task 13 · 160 → Tasks 2/3 · 162 → Task 10 · 163 → Task 10 · 169 → Task 13 · 175/181 → Task 2 · 188/189/196 → Task 12 · 201 → Task 13 · 202/209/219/226/234/248/254(button)/259/266/272 → Task 2 · 208/216/224 → Task 9 · 210 → Task 13 · 217 → Task 6 · 218 → Task 9 · 225 → Tasks 4/7 · 235 → Task 13 · 236 → Task 13 · 241 → Tasks 4/18 · 242 → Task 13 · 254(empty-state) → Task 13.

**P3:** 149/150/156/176/182/190/197/203/211/227/242/260/265 → batched into Tasks 10/11/12/13 or explicitly deferred in Task 20 with reason. Structure/over-limit → Tasks 14-19.

No type/name mismatches: `Badge`/`DayBadge` (Task 6), `ReviewModal`/`ConfirmModal` (Tasks 5/15), `outcomeToStatus`/`statusBadgeClass`/`simOutcomeClass` (Task 7), `convertTemplateToSelectedExercises` (Task 14), `useSyncExerciseEditsToHevy` (Task 15) are referenced consistently across the tasks that consume them.

**Coverage gap noted in audit:** `modal-weight-confirm` (not reached) is screenshotted and audited in Task 20 Step 2 before Phase 2 is considered complete.

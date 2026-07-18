# Frontend Audit Findings — Arcade Minimal

**Date started:** 2026-07-18
**Rubric:** `src/A2S.Web/src/AGENTS.md` (styling contract) + UX checklist in spec §3.
**Severity:** P1 breaks usability/legibility · P2 violates contract/inconsistent · P3 polish.

## Summary & fix order

### Counts

Every `- **[P1]**`/`[P2]`/`[P3]` line in the whole document (static + screen sections), recounted:

| Severity | Static findings | Screen findings | Total |
|---|---|---|---|
| P1 | 0 | 7 | **7** |
| P2 | 28 | 32 | **60** |
| P3 | 7 | 23 | **30** |
| **All** | **35** | **62** | **97** |

Grand total: **97 findings.** (Counts reflect the findings in the body — the "Static findings" and "Screen findings" sections. The verbatim P1 block immediately below is a duplicated copy of the 7 body P1 lines for reference and is not itself counted, so a raw grep of the whole file returns 7 + 7 = 14 `[P1]` lines.)

### Complete P1 list (verbatim)

- **[P1]** `workout-session` (390px) — The "Per Side" badge is a `rounded-full` pill so narrow it collapses to an oval/near-circle and wraps "Per / Side" onto two lines, breaking the badge shape and shoving the edit/swap icons; it reads as a rendering glitch mid-title on Single Arm Lat Pulldown and Cable Core Pallof Press. Off-token blue (`bg-blue-100 text-blue-700`) compounds it. Evidence: `workout-session--390.png`.
- **[P1]** `modal-progression` (390px) — The modal footer button row overflows the viewport horizontally: "CHANGE PROGRESSION TYPE" and "CLOSE" sit side-by-side wider than 390px, so CLOSE is clipped to "CLO…" and runs off the right edge — the dismiss control is partially unreachable/cut off. The two footer buttons must stack (or the labels shorten) at this width. Evidence: `modal-progression--390.png`.
- **[P1]** `modal-substitution` (390px) — Same footer overflow as the progression modal: "CANCEL" and "SUBSTITUTE EXERCISE" sit side-by-side wider than the viewport, so CANCEL is clipped to "…EL" at the left edge and SUBSTITUTE EXERCISE runs off the right. Both footer actions are partially cut off. Buttons must stack at 390px. Evidence: `modal-substitution--390.png`.
- **[P1]** `sign-up` — The ENTIRE page background is light/near-white (not just the Clerk card): the app shell renders a light-grey page with black "99 Strength" heading, whereas sign-in renders the correct near-black dark background. The two auth entry points are visually inconsistent with each other, and sign-up abandons the Arcade Minimal dark theme entirely — a full-page theme break, not just the embedded widget. Evidence: `sign-up--1440.png`, `sign-up--390.png` (contrast against `sign-in--1440.png`).
- **[P1]** `setup-wizard-3-exercises` (390px) — In "Your Program", the day columns collapse to a 2-up grid of very narrow cards where each exercise row loses its NAME entirely: rows show only the index badge, weight, reps and sets (e.g. "1 · 45kg · 6-10 reps · 3→5 sets") with no exercise title, making the program list unreadable/unidentifiable at 390px. Exercise names are present at 1440px, so the name is being squeezed out by the narrow mobile column. Evidence: `setup-wizard-3-exercises--390.png` (contrast `setup-wizard-3-exercises--1440.png`).
- **[P1]** `hevy` — The CONNECT HEVY primary button renders as a dark muted-brown fill with grey-on-brown label text (see crop) — the label is barely legible against the fill and the button reads as disabled despite being the page's primary CTA. This is a genuine contrast failure, distinct from (and worse than) the washed-out-label-on-orange retro bevel flagged elsewhere. Same on mobile. Evidence: `hevy--1440.png`, `hevy--390.png`.
- **[P1]** `hevy-data` — The CONNECT HEVY button carries the same dark muted-brown fill / illegible grey label as on the Hevy page (same button, same failure). Evidence: `hevy-data--1440.png`, `hevy-data--390.png`.

### Recommended fix grouping

Every finding is assigned to exactly one group below. The two out-of-scope backend observations (fresh-DB seeder short-circuit; concurrent first-login user-provisioning race, both under "Out-of-scope environment observations") are **not** in any group — they are outside this audit's fix scope and stay out of all three.

#### Group A — cross-cutting root-cause fixes (do first)

Ordered by how many downstream findings each unblocks (most first). Each item names the screen findings it resolves so Phase 2 (Group B) does not double-fix them.

1. **`button.tsx` Orbitron + `uppercase tracking-wide` root cause** — unblocks **~17 findings** (1 static root-cause P2 + the retro-button/nav-treatment instances across screens). Removing `font-[Orbitron,sans-serif] uppercase tracking-wide` from the Button base variant resolves: cross-screen retro-button + primary-button-treatment (139, 141, 142's button clause), dashboard (147), workout (154), workout-session (160), modal-progression (175), modal-substitution (181), setup-wizard-1 (202), setup-wizard-2 (209), setup-wizard-3 (219), setup-wizard-4 (226), programs (234), exercises button treatment, hevy (248), hevy-data (254), simulate (272), and the button-treatment-inconsistency findings on settings (259) and history (266). Also removes the banned `glow`/`shadow-primary` variant (part of 142). **This is the single highest-leverage fix in the audit.** NOTE: the CONNECT HEVY *contrast failure* P1s (hevy 247, hevy-data 253) and the retro-orange *washed-out-label* problems are fill/foreground-token issues that this font fix alone does NOT resolve — see item 2.
2. **Primary-button fill + label-contrast tokens** — unblocks **~4 findings** including 2 P1s: the CONNECT HEVY illegible dark-brown fill (hevy 247, hevy-data 253) and the bevelled retro-orange washed-out-label instances (cross-screen 141, and the primary-fill clauses in workout-session 160, modal-substitution 181, setup-wizard NEXT/CREATE 202/226). Retune the primary variant to a flat on-token fill with a legible foreground token.
3. **Dead `dark:` variant sweep** — unblocks **all 28 static P2 `dark:` findings** (lines 48-73) plus the off-token-blue day-badge and green-outcome instances that surface again on screens (setup-wizard-3 217, setup-wizard-4 green icon 225, programs green pill 235, exercises colour map 241). Mechanical removal per the contract ("remove on sight").
4. **Shared `ReviewModal`/`DecisionListModal` primitive** — unblocks **~6 findings**: collapses the four near-identical modals (Missing/Pulled/WeightDiscrepancy/WeightConfirmation, Structure obs + their dead-`dark:` clusters) and the footer-overflow P1s stack correctly once (modal-progression 174, modal-substitution 180) if the shared footer stacks at ≤390px.
5. **Shared `Badge`/day-number-badge primitive** — unblocks **~4 findings**: the "Per Side" pill P1 (workout-session 161), the triplicated blue day badge (Structure obs; DayColumnsView/SelectedExerciseCard/SimpleDayColumnsView), and setup-wizard-3 day badge (217). A single `<Badge>` with a min-width and token colours fixes the collapse and the off-token blue at once.
6. **Shared `outcomeToStatus()` classifier + token status badge** — unblocks **~3 findings**: the fragile `.includes()` outcome-colour logic in CompletionSummary + SimulationPage (Structure obs) and the off-token green/status colours they emit.
7. **Typography-scale application + spacing rhythm** — unblocks **~2 findings**: applies the token type scale so nav/headings stop using the display font (cross-screen 140 nav, dashboard headings 147) beyond what the button fix covers, and normalises `p-4`/`p-6` rhythm. Lowest-leverage Group A item; mostly reinforces items 1-2.

#### Group B — screen-by-screen (remaining per-screen fixes)

Ordered worst-first by severity count per screen. Findings already resolved by Group A are **excluded** here (noted per screen) to avoid double-fixing. Remaining items are the screen-specific defects Group A does not touch.

1. **setup-wizard-3-exercises** (P1×1, P2×4, P3×1) — after A: exercise-name squeezed out of 2-up mobile cards (215, P1); step-indicator "Confirm" clipped at 390px (216); ~20-24px edit/delete touch targets (218); orange filter-chip vs orange Add-button state collision (220). (Day-badge 217 and BACK/NEXT 219 handled by A.)
2. **workout-session** (P1 handled by A-5; remaining P2×2, P3×1) — ~24px edit/swap touch targets (162); floating palm avatar overlaps swap icon (163); LOG outline red vertical artifact (164). (Per-Side badge 161 by A-5; button 160 by A-1/A-2.)
3. **modal-progression** (P1 by A-4; remaining P3×1) — mixed tag styles on header row / sparse empty state (176).
4. **modal-substitution** (P1 by A-4; remaining P3×1) — heavy orange focus ring note (182, positive-leaning).
5. **sign-up** (P1×1, P2×1, P3×1) — full-page light theme break (195, P1); Clerk light card + non-primary Continue (196); palm avatar overlap (197).
6. **sign-in** (P2×2, P3×2) — Clerk light card on dark page (188); non-primary Continue button (189); empty-band framing (190); palm avatar clips footer (191).
7. **setup-wizard-4-confirm** (P2×3→1 after A, P3×1) — step-indicator "Confirm" clipped (224); edge-gutter + palm overlap (227). (Green icon 225 by A-3; CREATE PROGRAM 226 by A-1/A-2.)
8. **setup-wizard-2-template** (P2×2→1 after A, P3×2) — step-indicator overflow at 390px (208); no selected/checked affordance on template cards (210); palm avatar overlap (211). (BACK/NEXT 209 by A-1.)
9. **setup-wizard-1-welcome** (P2×2→1 after A, P3×1) — three-node vs four-node step rail inconsistency (201); palm avatar overlap (203). (BACK/NEXT 202 by A-1/A-2.)
10. **dashboard** (P2×2→1 after A, P3×2) — palm avatar obscures stat/progression cards at 390px (148); repetitive per-exercise empty-state wall (149); PR empty-state positive note (150). (Headings/CTA 147 by A-1/A-7.)
11. **workout** (P2×1 by A-1, P3×2) — dense day-column exercise separation (155); blurred next-week-preview scroll length (156). (Meta chips/labels 154 by A-1.)
12. **programs** (P2×1 by A-1, P3×2) — redundant dual status pills / off-token green (235); "FourDay-Day Split" data glitch (236). (Buttons 234 by A-1.)
13. **exercises** (P2×1 by A-3, P3×1) — 444-tile long scroll / no sticky filters (242). (Muscle-group colour map 241 by A-3.)
14. **nav-mobile** (P2×1 by A-1, P3×1) — active-item highlight reads as hover (169). (Menu ALL-CAPS 168 by A-1/A-7.)
15. **settings** (P2×1 by A-1, P3×1) — palm avatar overlaps Export button (260). (Button-treatment inconsistency 259 by A-1.)
16. **hevy** (P1 by A-2; remaining none screen-specific) — no residual Group B item (247 by A-2, 248 by A-1).
17. **hevy-data** (P1 by A-2; remaining P2×1) — left-aligned unstyled empty state, inconsistent with other empty states (254). (Button 253 by A-2.)
18. **simulate** (P2×1 by A-1; remaining none) — no residual Group B item (272 by A-1; results table/chart deferred, see Phase 2).
19. **cross-screen** (all P2/P3 handled by A) — no residual Group B item (139-142 by A-1/A-2/A-3).
20. **history** (P3×2) — off-token block legend dots (sanctioned file, polish only, 265); export/tab button-treatment inconsistency (266, by A-1).

#### Group C — SRP / structure refactors (behavior-preserving only)

File splits and extractions from the Structure/SRP observations and the over-500-line list. All are behavior-preserving; none change business logic.

- **`EditExercisesModal.tsx` (730 lines)** — extract the Hevy delete/recreate/resync orchestration into `useSyncExerciseEditsToHevy`; extract the `ExerciseEditState` derivation; leave the component markup-only. Also replace its bespoke inline "Remove Exercise" confirm modal (707-727) with the shared confirm primitive (relates to A-4).
- **`SetupWizard.tsx` (639 lines)** — move the template-to-`SelectedExercise` conversion (31-60+) into `lib/` as a unit-testable pure function.
- **`ExerciseConfigDialog.tsx` (513 lines)** — split marginally-over-limit dialog (contract completeness).
- **`ExerciseLibraryComponents.tsx`** — move `MUSCLE_GROUP_CONFIG`/`EQUIPMENT_CONFIG` styling half into a token-driven lookup (relates to A-3).
- **`SimulationPage.tsx` (694 lines, P3)** — over-limit dev/debug tooling page; lower priority (internal tool). Its outcome-string-to-colour switch also relates to A-6.
- **`types/workout.ts` (541 lines, P3)** and **`ExerciseConfigDialog.tsx` (513 lines, P3)** — marginal/low-risk over-limit; split for contract completeness.
- **Lint toolchain (P3, static "Root causes & toolchain")** — `npm run lint` fails with 60 pre-existing errors across ~25 files (unused vars in `tests/e2e/*`, `rules-of-hooks`, setState-in-effect incl. `Navbar.tsx:25`). Out of the styling-fix path but recorded here as a structural/toolchain cleanup to schedule alongside Group C; none are on lines touched by the consolidation. This is the one finding that is neither cross-cutting styling (A) nor a per-screen visual fix (B).
- Data/test/story files (`hevyExercises.ts` 3109, `workoutTemplates.ts` 1093, stories/test files) are out of scope for the component line-limit contract; `hevyExercises.ts` split is a maintainer-convenience note only, not required.

### Phase 2 sequencing recommendation

Execute **Group A first**, in the numbered order above — the `button.tsx` and dead-`dark:` fixes alone retire well over half the findings and remove the root causes that otherwise recur per-screen. **Then re-screenshot every audited screen at 1440px + 390px** and re-diff against `audit-screenshots/`: this is the primary re-verification point and confirms which Group B items genuinely remain versus which A silently resolved (the per-screen "handled by A" notes above are the checklist). **Next run Group B** worst-first, re-screenshotting each screen touched and confirming the 5 remaining P1-derived issues (name-squeeze 215, footer stacking 174/180 via A-4, full-page theme break 195) render correctly at 390px. **Finally Group C**, which is behavior-preserving: gate each refactor on `npm run build` + `npm test` staying green (246/249 baseline per Task 7) with no screenshot change expected. Re-verification points: (1) after A, full re-screenshot + build/test; (2) after each Group B screen, targeted re-screenshot; (3) after each Group C refactor, build/test parity with no visual diff.

**Coverage gap:** the `modal-weight-confirm` screen was **not reached** in this audit (it requires a completed session with a pending weight bump; see the screen section at the end of the doc). It carries no findings yet and must be screenshotted and audited as part of Phase 2 verification before this audit can be considered complete. Two other screens were only partially exercised on the seeded Week-1 account (`hevy-data` table-overflow, `simulate` results table/chart, `history` "Exercise Progress" charts — all deferred to connected/post-run passes).

## Static findings

### Hardcoded colours in components

Sweep: `grep -rnE "#[0-9a-fA-F]{6}\b|hsl\(\s*[0-9]|rgb\(" src/A2S.Web/src --include="*.tsx" --include="*.ts" | grep -v ".test." | grep -v "index.css"` — 4 hits (excluding one that's a doc comment, not a live literal).

- **[P3]** `src/A2S.Web/src/lib/blockColors.ts:15` — `1: '#3b82f6', // blue`. Evidence: `1: '#3b82f6', // blue`. This is a sanctioned centralised fixed-categorical-palette file (per contract, `lib/blockColors.ts` is allowed to hold literal values for training-block identity), but the values are raw Tailwind-blue/violet/pink hex, not theme tokens or `color-mix`. Contract explicitly permits this file to diverge from the token rule for categorical identity, so this is polish (e.g., consider deriving from `--color-neon-*` tokens for consistency) rather than a violation.
- **[P3]** `src/A2S.Web/src/lib/blockColors.ts:16` — `2: '#8b5cf6', // violet`. Same as above.
- **[P3]** `src/A2S.Web/src/lib/blockColors.ts:17` — `3: '#ec4899', // pink`. Same as above.
- No finding — `src/A2S.Web/src/lib/chartTheme.ts:9` — matched only because a doc comment contains the string `hsl(25 80% 45%)` as an example of what `index.css` holds; no live colour literal in the file. All exported values are `var(--color-*)` or `color-mix(...)` references, fully compliant with the contract's chart rules.

Note: `MUSCLE_GROUP_CONFIG` in `src/A2S.Web/src/features/exercises/ExerciseLibraryComponents.tsx` (lines 57-76) and many `dark:` sites below use *named* Tailwind colour utilities (`bg-orange-500/10`, `text-green-600`, etc.) rather than literal hex/hsl/rgb strings, so they don't match this regex — but they are still off-token colour usage. They're captured under the `dark:` sweep below and flagged again in Structure observations since the sweep patterns don't independently catch bare `text-green-600`-style classes without an accompanying `dark:` variant (e.g. `SimulationPage.tsx:340-348`, `CompletionSummary.tsx:111-121`).

### Dead `dark:` variants

Sweep: `grep -rn "dark:" src/A2S.Web/src --include="*.tsx"` — 126 hits across 19 files. Per contract, "there is no theme switcher and no `.dark`/`.apple-theme` class; a `dark:` Tailwind variant is always dead code — remove it on sight." All hits below are P2 except where noted. Counts per file:

| File | Hits |
|---|---|
| `features/exercises/ExerciseLibraryComponents.tsx` | 20 |
| `features/workout/ExerciseSubstitutionModal.tsx` | 15 |
| `features/workout/PulledSubstitutionsModal.tsx` | 14 |
| `features/workout/ExerciseCard.tsx` | 14 |
| `features/workout/WeightDiscrepancyModal.tsx` | 11 |
| `features/workout/MissingExercisesModal.tsx` | 10 |
| `features/workout/CompletionSummary.tsx` | 10 |
| `features/workout/EditExercisesModal.tsx` | 6 |
| `features/workout/EditExerciseConfigModal.tsx` | 5 |
| `components/shared/UndoConfirmationModal.tsx` | 5 |
| `features/workout/WorkoutHeader.tsx` | 3 |
| `features/workout/WeightConfirmationModal.tsx` | 3 |
| `features/workout/SessionRecoveryModal.tsx` | 2 |
| `features/workout/ExerciseSelectionV2/SelectedExerciseCard.tsx` | 2 |
| `features/workout/BlockSequenceEditor.tsx` | 2 (1 is prose in a comment, not live code — see below) |
| `features/workout/WorkoutDashboard.tsx` | 1 |
| `features/workout/ExerciseSelectionV2/SimpleDayColumnsView.tsx` | 1 |
| `features/workout/ExerciseSelectionV2/DayColumnsView.tsx` | 1 |
| `features/workout/DayCard.tsx` | 1 |

Representative hits (full detail; remaining hits in each file follow the same `bg-*/text-*/border-* dark:*` pattern and carry the same severity):

- **[P2]** `src/A2S.Web/src/components/shared/UndoConfirmationModal.tsx:46` — `<div className="relative bg-white dark:bg-zinc-900 ...">`. Evidence: `bg-white dark:bg-zinc-900`. `bg-white` itself is also off-token (not `bg-card`).
- **[P2]** `src/A2S.Web/src/components/shared/UndoConfirmationModal.tsx:60` — `bg-yellow-100 dark:bg-yellow-900/30` warning panel.
- **[P2]** `src/A2S.Web/src/components/shared/UndoConfirmationModal.tsx:62,66,69` — `text-yellow-600 dark:text-yellow-400` / `text-yellow-800 dark:text-yellow-200` / `text-yellow-700 dark:text-yellow-300` on warning icon/text.
- **[P2]** `src/A2S.Web/src/features/exercises/ExerciseLibraryComponents.tsx:57-76` — entire `MUSCLE_GROUP_CONFIG` map (20 entries) uses `text-{colour}-600 dark:text-{colour}-400` per muscle group.
- **[P2]** `src/A2S.Web/src/features/workout/BlockSequenceEditor.tsx:121` — `className="ml-1 p-0.5 rounded-full hover:bg-black/10 dark:hover:bg-white/10 transition-colors"`. Live dead code.
- No finding (informational) — `src/A2S.Web/src/features/workout/BlockSequenceEditor.tsx:25` — matched the grep but is prose inside a doc comment ("...replaces the old Tailwind `dark:` class map, which only changed on OSRS"), documenting that this file was already migrated to `lib/blockColors.ts` + `color-mix`. Not live code; no fix needed.
- **[P2]** `src/A2S.Web/src/features/workout/CompletionSummary.tsx:113,116,119,121` — `getOutcomeStyle()` returns `text-green-600 bg-green-100 dark:bg-green-900/30` / red / blue / yellow variants.
- **[P2]** `src/A2S.Web/src/features/workout/CompletionSummary.tsx:142,150` — completion header `border-green-500 bg-green-50 dark:bg-green-950/20`, `text-green-700 dark:text-green-400`.
- **[P2]** `src/A2S.Web/src/features/workout/CompletionSummary.tsx:257,258,267` — "New Weights Next Session" card `border-amber-400 bg-amber-50 dark:bg-amber-950/20`.
- **[P2]** `src/A2S.Web/src/features/workout/CompletionSummary.tsx:328` — deload badge `bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400`.
- **[P2]** `src/A2S.Web/src/features/workout/DayCard.tsx:186` — `border-green-500 bg-green-50 dark:bg-green-950` completed-day styling.
- **[P2]** `src/A2S.Web/src/features/workout/EditExerciseConfigModal.tsx:317-318,399-400` — info panels `bg-blue-50 dark:bg-blue-950/30 border-blue-200 dark:border-blue-800`, `text-blue-700 dark:text-blue-300`.
- **[P2]** `src/A2S.Web/src/features/workout/EditExerciseConfigModal.tsx:446` — swap-toggle active state `border-orange-400 bg-orange-50 text-orange-700 dark:bg-orange-950/30 dark:text-orange-300 dark:border-orange-700`.
- **[P2]** `src/A2S.Web/src/features/workout/EditExercisesModal.tsx:408,580-581,641-642,674` — "Swapping" badge and swap-config panels, same orange `dark:` pattern as EditExerciseConfigModal (duplicated across two files — see Structure section).
- **[P2]** `src/A2S.Web/src/features/workout/ExerciseCard.tsx:55,73,93,101,106,111,120,183-188,201,207,222,240` — 14 hits: completed-card `bg-green-50 dark:bg-green-950/20`, unilateral badge `bg-blue-100 dark:bg-blue-900/30`, AMRAP badge `bg-yellow-100/amber-100`, AMRAP set gradient `from-orange-100 to-amber-100 dark:from-orange-950/40 dark:to-amber-950/40`, AMRAP input borders `border-orange-300 dark:border-orange-700`.
- **[P2]** `src/A2S.Web/src/features/workout/ExerciseSelectionV2/DayColumnsView.tsx:76` — day-number badge `bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400`.
- **[P2]** `src/A2S.Web/src/features/workout/ExerciseSelectionV2/SelectedExerciseCard.tsx:34,140` — same blue badge pattern, duplicated with `DayColumnsView.tsx`/`SimpleDayColumnsView.tsx`.
- **[P2]** `src/A2S.Web/src/features/workout/ExerciseSelectionV2/SimpleDayColumnsView.tsx:32` — same blue badge pattern again (3rd copy).
- **[P2]** `src/A2S.Web/src/features/workout/ExerciseSubstitutionModal.tsx:211,231-308` — 15 hits: yellow warning badge/panel, sets/reps inputs `bg-white dark:bg-zinc-800`.
- **[P2]** `src/A2S.Web/src/features/workout/MissingExercisesModal.tsx:47-100` — 10 hits: blue header `bg-blue-100 dark:bg-blue-900`, body `bg-white dark:bg-zinc-900`, list rows `bg-zinc-50 dark:bg-zinc-800`, selection states `bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200` (duplicated 2x in this file, and again in `PulledSubstitutionsModal.tsx`/`WeightDiscrepancyModal.tsx`).
- **[P2]** `src/A2S.Web/src/features/workout/PulledSubstitutionsModal.tsx:41-109` — 14 hits, same structural pattern as `MissingExercisesModal.tsx` (near-identical modal — see Structure section) plus red/green diff text `text-red-600 dark:text-red-400` / `text-green-600 dark:text-green-400`.
- **[P2]** `src/A2S.Web/src/features/workout/SessionRecoveryModal.tsx:35-36` — `bg-blue-100 dark:bg-blue-900`, `text-blue-600 dark:text-blue-400`.
- **[P2]** `src/A2S.Web/src/features/workout/WeightConfirmationModal.tsx:46-53` — `bg-blue-100 dark:bg-blue-900` header, `text-blue-700 dark:text-blue-300` description — same modal-header pattern as `MissingExercisesModal.tsx`/`WeightDiscrepancyModal.tsx`/`PulledSubstitutionsModal.tsx` (4th copy).
- **[P2]** `src/A2S.Web/src/features/workout/WeightDiscrepancyModal.tsx:52-127` — 11 hits, same pattern family as above three modals (orange variant).
- **[P2]** `src/A2S.Web/src/features/workout/WorkoutDashboard.tsx:157` — `bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300` status pill.
- **[P2]** `src/A2S.Web/src/features/workout/WorkoutHeader.tsx:55-62` — `bg-blue-50 dark:bg-blue-950/30 border-blue-200 dark:border-blue-800`, `text-blue-700 dark:text-blue-300`, `text-blue-600 dark:text-blue-400`.

### Arbitrary Tailwind colour values

Sweep: `grep -rnE "\[(#|hsl|rgb)" src/A2S.Web/src --include="*.tsx"` — 0 hits.

None found.

### Files over 500 lines

Sweep: `find src/A2S.Web/src -name "*.tsx" -o -name "*.ts" | xargs wc -l | awk '$1 > 500 {print}' | sort -rn` — 11 files:

| Lines | File |
|---|---|
| 3109 | `src/A2S.Web/src/data/hevyExercises.ts` |
| 1093 | `src/A2S.Web/src/data/workoutTemplates.ts` |
| 1029 | `src/A2S.Web/src/features/workout/ExerciseSelectionV2/ExerciseSelectionV2.stories.tsx` |
| 730 | `src/A2S.Web/src/features/workout/EditExercisesModal.tsx` |
| 694 | `src/A2S.Web/src/features/workout/SimulationPage.tsx` |
| 639 | `src/A2S.Web/src/features/workout/SetupWizard.tsx` |
| 616 | `src/A2S.Web/src/hooks/useExerciseSelection.test.ts` |
| 541 | `src/A2S.Web/src/types/workout.ts` |
| 532 | `src/A2S.Web/src/api/workouts.test.ts` |
| 522 | `src/A2S.Web/src/hooks/useWorkouts.test.tsx` |
| 513 | `src/A2S.Web/src/features/workout/ExerciseSelectionV2/ExerciseConfigDialog.tsx` |

- **[P2]** `src/A2S.Web/src/features/workout/EditExercisesModal.tsx` — 730 lines, over the 500-line contract limit. See Structure section.
- **[P3]** `src/A2S.Web/src/features/workout/SimulationPage.tsx` — 694 lines, dev/debug tooling page; over limit but lower priority (internal tool, not a user-facing screen).
- **[P2]** `src/A2S.Web/src/features/workout/SetupWizard.tsx` — 639 lines, over limit. See Structure section.
- **[P3]** `src/A2S.Web/src/types/workout.ts` — 541 lines, pure type/interface declarations (DTOs, enums). Line-count overage is low-risk here since there's no markup/logic mixing — flagging only for contract completeness.
- **[P3]** `src/A2S.Web/src/features/workout/ExerciseSelectionV2/ExerciseConfigDialog.tsx` — 513 lines, marginally over limit.
- No finding (out of scope for line-limit contract) — `src/A2S.Web/src/data/hevyExercises.ts` (3109 lines) and `src/A2S.Web/src/data/workoutTemplates.ts` (1093 lines) are static data tables (exercise catalogue / template defs), not components; the 500-line contract targets component files. Flagged for awareness only, not a styling-contract violation.
- No finding (out of scope) — `ExerciseSelectionV2.stories.tsx` (1029 lines, Storybook stories), `useExerciseSelection.test.ts` (616), `workouts.test.ts` (532), `useWorkouts.test.tsx` (522) are test/story files, not production components; contract's 500-line rule is aimed at app code.

### Structure / SRP observations

- `src/A2S.Web/src/features/workout/EditExercisesModal.tsx` (730 lines) mixes three concerns in one file: (1) a large `ExerciseEditState` derivation/reducer-like block (lines 80-167) computing swap defaults and change-tracking, (2) a multi-branch async save orchestrator (`handleSave`, lines 181-359) that does REST substitution calls, weight updates, and a full Hevy delete/recreate/resync flow with its own toast lifecycle, and (3) ~370 lines of nested conditional JSX for expand/collapse, swap forms, and a second inline confirmation modal (lines 707-727). The Hevy-sync orchestration (delete routine, wait 1s, get-or-create folder, refetch workout, resync) duplicates logic that likely also lives in the Hevy sync service/hooks elsewhere — a candidate to extract into a hook (e.g. `useSyncExerciseEditsToHevy`) so the component is markup-only.
- `src/A2S.Web/src/features/workout/EditExercisesModal.tsx:707-727` — the "Remove Exercise" confirmation is a bespoke inline modal (raw `fixed inset-0 ... bg-black/70` div) duplicating the same backdrop/card/button-row structure already implemented as reusable in `components/shared/UndoConfirmationModal.tsx`; should be a shared `ConfirmModal` primitive instead of being re-authored per feature.
- `src/A2S.Web/src/features/workout/SetupWizard.tsx` (639 lines) combines wizard-step state machine logic, template-to-`SelectedExercise` conversion (a fairly involved mapping function, lines 31-60+), and the full multi-step form markup in one component — the template-conversion logic reads as pure data transformation that could live in `lib/` and be unit-tested independently of the component.
- Four near-identical "review/decision" modals — `MissingExercisesModal.tsx`, `PulledSubstitutionsModal.tsx`, `WeightDiscrepancyModal.tsx`, `WeightConfirmationModal.tsx` — repeat the same structural skeleton (colour-tinted `DialogHeader`, scrollable `bg-white dark:bg-zinc-900` body, per-row `bg-zinc-50 dark:bg-zinc-800` card, two-choice selection buttons with the identical `bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200` class string, footer button row). This is a strong candidate for a shared `ReviewModal`/`DecisionListModal` primitive — today each file re-implements the same layout with only the accent colour and copy changed, and each one independently carries the same set of dead `dark:` variants.
- Three components — `ExerciseSelectionV2/DayColumnsView.tsx:76`, `SelectedExerciseCard.tsx:34,140`, `SimpleDayColumnsView.tsx:32` — each define their own copy of a circular day-number/index badge (`w-7 h-7 rounded-full bg-blue-100 ... text-blue-700 ...`) instead of sharing one badge component.
- `src/A2S.Web/src/features/exercises/ExerciseLibraryComponents.tsx:56-90` defines `MUSCLE_GROUP_CONFIG`/`EQUIPMENT_CONFIG` as large literal maps mixing display data (label, icon) with styling data (Tailwind colour classes) inline in a components file; the styling half belongs in a token-driven lookup (or the component should derive colour from a smaller fixed palette) rather than one bespoke Tailwind colour pairing per muscle group.
- `src/A2S.Web/src/features/workout/CompletionSummary.tsx:111-135` implements outcome classification via `.toLowerCase().includes(...)` string matching on a free-text `change` string to pick both a colour class (`getOutcomeStyle`) and a label (`getOutcomeLabel`) — fragile (matches substrings, order-dependent, easy to regress by rewording a backend message) and duplicates a similar pattern in `SimulationPage.tsx:337-348` (outcome-string-to-colour-class switch) using non-token Tailwind colours (`text-green-600`, `text-yellow-600`, `text-blue-500`) rather than `chartColors`/status tokens. A shared `outcomeToStatus(change)` classifier plus a token-based status-badge component would remove both duplication and the off-token colours.
- `src/A2S.Web/src/features/workout/ExerciseCard.tsx` (see also EditExerciseConfigModal.tsx, EditExercisesModal.tsx swap panels) mixes data logic (collapse-on-prefill effect, uniform-weight summary formatting) with a long run of conditional badge markup (unilateral/AMRAP/completed/substituted states each hand-rolled with their own colour+dark: pair) — the AMRAP/unilateral/completed badges look like they should be a single small `<Badge variant=.../>` primitive instead of four different inline className conditionals.
- `src/A2S.Web/src/features/workout/BlockSequenceEditor.tsx:27-34` (`blockChipStyle`) is a good example of the *correct* pattern (inline `style` object built from `getBlockColor()` + `color-mix`) — included here only as a positive reference point for what the modal/badge components above should migrate toward, not a violation.
- `src/A2S.Web/src/data/hevyExercises.ts` (3109 lines) is a single flat exported array/object of exercise catalogue data with no logic — not a component SRP issue, but its size makes it a likely editor-performance and diff-noise concern; splitting by muscle group or equipment (or generating it) is worth a note for future maintainers even though it's out of scope for the styling contract.

### Root causes & toolchain (Task 7 verification)

- **[P2]** `src/A2S.Web/src/components/ui/button.tsx:8` — the Button base variant hardcodes `font-[Orbitron,sans-serif]` plus `uppercase tracking-wide`, which is the root cause of every "retro uppercase button" screen finding below. Orbitron is no longer loaded (falls back to sans-serif) but the arbitrary font-family and forced uppercase violate the contract's typography rules; fix once here and the button findings across all screens resolve together. Evidence: `grep -n "Orbitron" src/A2S.Web/src/components/ui/button.tsx`.
- **[P3]** `src/A2S.Web` — `npm run lint` fails with 60 pre-existing errors across ~25 files (unused vars in `tests/e2e/*`, `react-hooks/rules-of-hooks` in `tests/e2e/fixtures/auth-fixture.ts:266`, setState-in-effect errors incl. `Navbar.tsx:25`). Verified none are on lines touched by the consolidation (Tasks 1–2); recorded here per plan instead of fixed. Evidence: lint run 2026-07-18.
- Verification summary (Task 7): `npm run build` ✅; `npm test` 246/249 ✅ (3 pre-existing `ExerciseLibraryPage.test.tsx` failures, `useHevy` provider wrapper issue, confirmed failing on base commit); no decorative-font glyphs (Press Start 2P / VT323 / RuneScape) or scanline overlay visible in any Task 6 screenshot; no black-on-black/illegible screen observed except the specific contrast findings recorded below.

### Out-of-scope environment observations (backend — not part of this audit's fix scope)

- Fresh-database bootstrap is broken: `ExerciseDefinitionSeeder.SeedAsync` short-circuits when `ExerciseDefinitions` is non-empty, and migration `20260615210530_SeedCableCorePallofPress` always inserts one row — so a brand-new migrated DB ends up with exactly 1 exercise and every workout create 400s ("Exercise template ... was not found"). Workaround used for this audit: delete the row, restart API to reseed (445 definitions). Both Pallof spellings now exist in `exercise-library.json`, so the migration row is redundant on fresh DBs.
- Concurrent first-login requests race user auto-provisioning: several parallel API calls each tried to insert the same user, producing `23505 duplicate key ... PK_Users` errors in the API log (harmless to the winner, noisy and wasteful).

## Screen findings

_Screenshots referenced below live in the repo-root `audit-screenshots/` directory (untracked by design)._

_Visual audit of core screens (Task 6a), 2026-07-18. Screenshots in `audit-screenshots/` at desktop 1440px + mobile 390px, fullPage. AUDIT ONLY — no code changed. Where a visual defect has a clear code root, the root is named for the fix phase, but the finding is anchored to screenshot evidence per the format._

### Cross-screen consistency

- **[P2]** `cross-screen` — Retro leftover: every `<Button>` renders ALL-CAPS in the geometric/blocky Orbitron display font with letterspacing (contract bans `uppercase` and mandates system fonts). Visible on START W1 D1 WORKOUT, LOG, CANCEL, COMPLETE WORKOUT, SUBSTITUTE EXERCISE, CHANGE PROGRESSION TYPE, CLOSE across all screens. Root: `components/ui/button.tsx:8` base class `uppercase tracking-wide font-[Orbitron,sans-serif]`. Evidence: `workout-session--1440.png`, `modal-substitution--1440.png`, `modal-progression--1440.png`.
- **[P2]** `cross-screen` — Retro leftover: the entire top nav (brand "STRENGTH" + every link DASHBOARD/WORKOUT/HISTORY/… + "PLAYER") is ALL-CAPS letterspaced display font; contract permits all-caps only for tiny eyebrow captions, not primary nav. Same on the mobile menu. Root: `components/layout/Navbar.tsx:60,73,108,145,160`. Evidence: `dashboard--1440.png`, `nav-mobile--390.png`.
- **[P2]** `cross-screen` — Primary buttons (START WORKOUT, COMPLETE WORKOUT, SUBSTITUTE EXERCISE) render with a bevelled/gradient-looking burnt-orange fill plus low-contrast washed-out label text, reading as a retro "arcade button" rather than the flat minimal surface the contract calls for; the label contrast against the orange fill is noticeably weak. Evidence: `workout-session--1440.png`, `modal-substitution--1440.png`.
- **[P3]** `cross-screen` — Off-token literal colours persist in shared chrome: badges use `bg-blue-100/text-blue-700` (the "Per Side" pill), status/outcome text uses named Tailwind colours, and buttons use `text-white`/`border-gray-500`/`text-gray-300` instead of tokens; a banned `glow` button variant (`shadow-primary/25→/40`) still exists in `button.tsx`. Cosmetically consistent but off-contract; catalogued in the Static findings above. Evidence: `workout-session--1440.png`.
- Note (positive): dashboard, workout, and session share one card language — `bg-card` + 1px border + `rounded-lg`, no resting shadows, consistent `p-4`/`p-6` padding and heading scale. Card style is consistent across the three; the deviations are the button/nav typography above, not the surfaces.

### dashboard

- **[P2]** `dashboard` — Hero "Welcome back, big" and section headings ("Quick Stats", "Current Program", "This Week's Training", "Exercise Progression", "Personal Records") render in the blocky display font, and the START W1 D1 WORKOUT CTA is ALL-CAPS Orbitron — same retro leftover as cross-screen. Evidence: `dashboard--1440.png`, `dashboard--390.png`.
- **[P2]** `dashboard` (390px) — The floating "island/palm" avatar widget overlaps and obscures card content: it sits on top of the "Workouts Done" stat in the Quick Stats card at 390px, and over the Exercise Progression cards further down. A fixed decorative overlay covering real data is both off-contract (glow/decorative element) and a mild usability hit. Evidence: `dashboard--390.png`.
- **[P3]** `dashboard` — The "Exercise Progression" section renders ~20 near-identical cards each reading "Not enough data yet (need 2+/3+ sessions)"; the empty-state repeats verbatim per exercise, producing a very long, low-information wall on both breakpoints. An aggregated empty state would cut the density. Evidence: `dashboard--1440.png`.
- **[P3]** `dashboard` — Empty "Personal Records" state is centred with a muted glyph + two-line copy and reads clean; noted only as the correct empty-state pattern the Exercise Progression section should follow. Evidence: `dashboard--1440.png`.

### workout

- **[P2]** `workout` — Retro leftover: page-header meta chips "MANAGE BLOCKS" and "» 21 weeks", the block toggles, and every "UPCOMING" footer label render in the ALL-CAPS blocky display font. Evidence: `workout--1440.png`.
- **[P3]** `workout` — The four "This Week's Training" day columns are dense stacks of Weight/Sets/Reps/Target Sets label:value pairs with no per-exercise separation beyond spacing; at 390px this compresses into a long, hard-to-scan run where exercise boundaries are easy to lose. Consider a divider or heavier exercise-name weight. Evidence: `workout--390.png`, `workout--1440.png`.
- **[P3]** `workout` — "Next Week Preview" cards are intentionally blurred/locked with a padlock; the lock affordance is clear, but at 390px the four blurred cards add substantial scroll length for a preview the user can't act on. Minor density note. Evidence: `workout--390.png`.

### workout-session

- **[P2]** `workout-session` — Retro leftover: CANCEL, LOG (×15), and COMPLETE WORKOUT are ALL-CAPS Orbitron; COMPLETE WORKOUT additionally shows the bevelled retro-orange fill with washed-out label. This is the primary mid-workout action surface, so the low-contrast label is the most consequential instance. Evidence: `workout-session--1440.png`, `workout-session--390.png`.
- **[P1]** `workout-session` (390px) — The "Per Side" badge is a `rounded-full` pill so narrow it collapses to an oval/near-circle and wraps "Per / Side" onto two lines, breaking the badge shape and shoving the edit/swap icons; it reads as a rendering glitch mid-title on Single Arm Lat Pulldown and Cable Core Pallof Press. Off-token blue (`bg-blue-100 text-blue-700`) compounds it. Evidence: `workout-session--390.png`.
- **[P2]** `workout-session` (390px) — The per-exercise edit (pencil) and swap (arrows) icon buttons are ~24px targets, under the 44px minimum; two small unlabelled icons sit close together in the card header, easy to mis-tap mid-workout. Evidence: `workout-session--390.png`.
- **[P2]** `workout-session` — The floating island/palm avatar overlaps the swap icon on the Crucifix Tricep Pulldown card (390px) and floats mid-card on desktop, obscuring an interactive control. Evidence: `workout-session--390.png`, `workout-session--1440.png`.
- **[P3]** `workout-session` — LOG (outline variant) buttons show a thin red vertical artifact at the right of the label on desktop; appears to be a rendering/caret artifact from the outline border and is cosmetically distracting when repeated ×15. Evidence: `workout-session--1440.png`.

### nav-mobile

- **[P2]** `nav-mobile` — Every menu item (DASHBOARD/WORKOUT/HISTORY/PROGRAMS/EXERCISES/SIMULATOR/HEVY/HEVY DATA/SETTINGS) and the "PLAYER" label render ALL-CAPS in the blocky display font — retro leftover, same root as the desktop nav. Evidence: `nav-mobile--390.png`.
- **[P3]** `nav-mobile` — The active item (DASHBOARD) uses a full-width burnt-orange-tinted highlight bar; the tint is quite dark/muted and the active state reads more like a hover than a clear "you are here". Minor hierarchy polish. Evidence: `nav-mobile--390.png`.
- Menu row heights are generous (comfortably ≥44px) and spacing is clean; no touch-target issue in the menu itself.

### modal-progression

- **[P1]** `modal-progression` (390px) — The modal footer button row overflows the viewport horizontally: "CHANGE PROGRESSION TYPE" and "CLOSE" sit side-by-side wider than 390px, so CLOSE is clipped to "CLO…" and runs off the right edge — the dismiss control is partially unreachable/cut off. The two footer buttons must stack (or the labels shorten) at this width. Evidence: `modal-progression--390.png`.
- **[P2]** `modal-progression` — CHANGE PROGRESSION TYPE and CLOSE are ALL-CAPS Orbitron (retro leftover). Evidence: `modal-progression--1440.png`.
- **[P3]** `modal-progression` — The "No completed weeks yet…" empty-state body is a single centred muted line in an otherwise tall panel; adequate but sparse. The "Reps Per Set" tag is orange and "Day 1" tag grey — two differently-styled tags on one header row is a minor inconsistency. Evidence: `modal-progression--1440.png`.

### modal-substitution

- **[P1]** `modal-substitution` (390px) — Same footer overflow as the progression modal: "CANCEL" and "SUBSTITUTE EXERCISE" sit side-by-side wider than the viewport, so CANCEL is clipped to "…EL" at the left edge and SUBSTITUTE EXERCISE runs off the right. Both footer actions are partially cut off. Buttons must stack at 390px. Evidence: `modal-substitution--390.png`.
- **[P2]** `modal-substitution` — CANCEL and SUBSTITUTE EXERCISE are ALL-CAPS Orbitron and the primary shows the bevelled retro-orange fill with washed-out label (retro leftover). Evidence: `modal-substitution--1440.png`.
- **[P3]** `modal-substitution` — The exercise search results list is a good, consistent card pattern (name + right-aligned equipment tag + muted "Hevy: …" line); no issue with the list itself. The search input's orange focus ring is heavy but on-brand. Evidence: `modal-substitution--1440.png`.

_Auth + setup-wizard flows (Task 6b), 2026-07-18. Screenshots in `audit-screenshots/` at desktop 1440px + mobile 390px, fullPage. AUDIT ONLY — no code changed. Sign-in/sign-up are Clerk-hosted components; findings judge their visual integration with the app's dark theme, not Clerk internals. Retro-leftover button/nav findings share the same `button.tsx` root already catalogued under Cross-screen; instances below are anchored to these screens' evidence per the format._

### sign-in

- **[P2]** `sign-in` — The Clerk card is rendered in Clerk's default LIGHT appearance (near-white card, black-on-white text, white social buttons, light footer) dropped onto the app's near-black branded background. It reads as a foreign light widget on a dark page — brand incoherence with the Arcade Minimal dark theme. Clerk's `appearance`/`baseTheme: dark` (or CSS-var elements theming) is not applied. Evidence: `sign-in--1440.png`.
- **[P2]** `sign-in` — The Clerk "Continue" primary button is a near-black bevelled/gradient pill (Clerk default) rather than the app's burnt-orange primary; the only orange accents on the whole card are the "Sign up" link, "Development mode" label, and the brand logo, so the auth surface does not carry the app's primary colour at all. Evidence: `sign-in--1440.png`, `sign-up--1440.png`.
- **[P3]** `sign-in` — The "99 Strength" wordmark + "Track your strength journey" tagline above the card render in the system-font display style (correct), but the surrounding dark page has a large empty band between the tagline and the card on desktop; the light card floating in a tall dark void reads as under-designed framing rather than a deliberate centred auth layout. Evidence: `sign-in--1440.png`.
- **[P3]** `sign-in` (390px) — The floating palm/island avatar widget sits fixed at the bottom-right and overlaps the footer "…Powered by Average to Sava[ge]" line, clipping the tagline text. Decorative overlay over real text, same fixed widget flagged elsewhere. Evidence: `sign-in--390.png`.

### sign-up

- **[P1]** `sign-up` — The ENTIRE page background is light/near-white (not just the Clerk card): the app shell renders a light-grey page with black "99 Strength" heading, whereas sign-in renders the correct near-black dark background. The two auth entry points are visually inconsistent with each other, and sign-up abandons the Arcade Minimal dark theme entirely — a full-page theme break, not just the embedded widget. Evidence: `sign-up--1440.png`, `sign-up--390.png` (contrast against `sign-in--1440.png`).
- **[P2]** `sign-up` — As with sign-in, the Clerk card uses Clerk's default light appearance and a near-black "Continue" button rather than the app's burnt-orange primary; because the page shell is also light here, there is zero dark-theme or primary-colour presence on the screen. Evidence: `sign-up--1440.png`.
- **[P3]** `sign-up` (390px) — Palm/island avatar overlaps the "Secured by clerk / Development mode" footer of the card at the bottom-right. Same fixed-widget overlap. Evidence: `sign-up--390.png`.

### setup-wizard-1-welcome

- **[P2]** `setup-wizard-1-welcome` — The step indicator shows only THREE nodes here (Start · Exercises · Confirm) but every subsequent step shows FOUR (Start · Template · Exercises · Confirm). The "Template" node appears/disappears depending on step, so the progress rail changes length and node count between steps — the wizard does not read as one fixed-length flow. Evidence: `setup-wizard-1-welcome--1440.png` (contrast `setup-wizard-2-template--1440.png`).
- **[P2]** `setup-wizard-1-welcome` — BACK and NEXT footer buttons are ALL-CAPS in the blocky Orbitron display font with letterspacing (contract bans uppercase headings/labels + mandates system fonts); NEXT additionally shows the bevelled/gradient retro-orange fill with washed-out low-contrast label. Same `button.tsx` root as Cross-screen. Evidence: `setup-wizard-1-welcome--1440.png`.
- **[P3]** `setup-wizard-1-welcome` (390px) — The fixed palm/island avatar overlaps the "Build from Scratch" choice card, sitting on top of card content in the top-right corner. Decorative overlay over an interactive selection card. Evidence: `setup-wizard-1-welcome--390.png`.
- Note (positive): the two choice cards ("Start from Template" / "Build from Scratch") use the correct `bg-card` + 1px border + `rounded-lg` surface, tokened orange icon tiles, and consistent heading/body scale — on-contract surface language.

### setup-wizard-2-template

- **[P2]** `setup-wizard-2-template` (390px) — The step indicator overflows the 390px viewport on the right: the final "Confirm" node + label is clipped at the right edge (the circle and its connector run past the container). The 4-node rail does not fit at 390px and is not made scrollable/wrapped/compacted. Evidence: `setup-wizard-2-template--390.png`.
- **[P2]** `setup-wizard-2-template` — BACK/NEXT are ALL-CAPS Orbitron with the bevelled retro-orange NEXT fill (same root as above). Evidence: `setup-wizard-2-template--1440.png`.
- **[P3]** `setup-wizard-2-template` — The selectable template cards carry no visible selected/checked affordance in the screenshot (no radio, check, or ring on a chosen card); a user cannot tell from the static frame which template is active before pressing NEXT. Minor selection-affordance polish. Evidence: `setup-wizard-2-template--1440.png`.
- **[P3]** `setup-wizard-2-template` (390px) — Palm/island avatar overlaps the "Big Daves Bonanza" template card body. Same fixed widget. Evidence: `setup-wizard-2-template--390.png`.

### setup-wizard-3-exercises

- **[P1]** `setup-wizard-3-exercises` (390px) — In "Your Program", the day columns collapse to a 2-up grid of very narrow cards where each exercise row loses its NAME entirely: rows show only the index badge, weight, reps and sets (e.g. "1 · 45kg · 6-10 reps · 3→5 sets") with no exercise title, making the program list unreadable/unidentifiable at 390px. Exercise names are present at 1440px, so the name is being squeezed out by the narrow mobile column. Evidence: `setup-wizard-3-exercises--390.png` (contrast `setup-wizard-3-exercises--1440.png`).
- **[P2]** `setup-wizard-3-exercises` (390px) — The step indicator's "Confirm" node is clipped off the right edge, same overflow as step 2. Evidence: `setup-wizard-3-exercises--390.png`.
- **[P2]** `setup-wizard-3-exercises` — The Day-column header index badges (circled 1/2/3/4) render as off-token blue `rounded-full` circles rather than a tokened badge — same off-token blue day-badge pattern catalogued in Static findings (`DayColumnsView`/`SelectedExerciseCard`). Visible on both breakpoints. Evidence: `setup-wizard-3-exercises--1440.png`, `setup-wizard-3-exercises--390.png`.
- **[P2]** `setup-wizard-3-exercises` (390px) — The per-exercise edit (pencil) and delete (trash) icon buttons on each program row are ~20-24px targets sitting close together, under the 44px minimum; on the cramped 2-up mobile cards they are easy to mis-tap. Evidence: `setup-wizard-3-exercises--390.png`.
- **[P2]** `setup-wizard-3-exercises` — BACK/NEXT are ALL-CAPS Orbitron with bevelled retro-orange NEXT (same root). Evidence: `setup-wizard-3-exercises--1440.png`.
- **[P3]** `setup-wizard-3-exercises` — The equipment filter chips ("All (50)", "Dumbbell (8)", …) and the "Add" buttons use the burnt-orange fill; the active "All" chip and every "Add" button are the same solid orange, so the selected-filter state is not distinguishable from the always-orange Add actions — minor state-vs-action colour collision. Evidence: `setup-wizard-3-exercises--1440.png`.

### setup-wizard-4-confirm

- **[P2]** `setup-wizard-4-confirm` (390px) — The step indicator's final "Confirm" node/label is clipped at the right edge, same 4-node overflow as steps 2 and 3. Evidence: `setup-wizard-4-confirm--390.png`.
- **[P2]** `setup-wizard-4-confirm` — The success/check header icon uses an off-token green tint (`bg-green-*`/`text-green-*` family) rather than a theme token — off-contract colour, consistent with the green outcome colours flagged in Static findings. Evidence: `setup-wizard-4-confirm--1440.png`.
- **[P2]** `setup-wizard-4-confirm` — CREATE PROGRAM (and BACK) are ALL-CAPS Orbitron; CREATE PROGRAM shows the bevelled retro-orange fill with washed-out label — the primary commit action of the whole flow, so the low-contrast label is the most consequential instance here. Evidence: `setup-wizard-4-confirm--1440.png`, `setup-wizard-4-confirm--390.png`.
- **[P3]** `setup-wizard-4-confirm` (390px) — The CREATE PROGRAM footer button runs to (and appears to touch/slightly exceed) the right viewport edge with minimal gutter, and the fixed palm avatar overlaps the Day 3 exercise list. Minor edge-gutter + decorative-overlap polish. Evidence: `setup-wizard-4-confirm--390.png`.
- Note (positive): the Review step's "Program Details" (Name/Variant/Duration) and per-day "Selected Exercises" list use consistent `bg-card` surfaces, tokened orange day headers and the correct heading/caption scale; the review content itself is coherent and on-contract — the deviations are the button/step-indicator/green-icon items above.

_Secondary pages (Task 6c), 2026-07-18. Screenshots in `audit-screenshots/` at desktop 1440px + mobile 390px, fullPage. AUDIT ONLY — no code changed. The seeded account is on Week 1 with no history, so empty states are exercised throughout. Retro-leftover button/nav findings share the `button.tsx`/`Navbar.tsx` roots already catalogued under Cross-screen; instances below are anchored to these screens' evidence per the format._

### programs

- **[P2]** `programs` — Retro leftover: CREATE NEW PROGRAM, VIEW WORKOUT and DELETE render ALL-CAPS in the blocky Orbitron display font with letterspacing; CREATE NEW PROGRAM and VIEW WORKOUT additionally show the bevelled/gradient retro-orange fill (with a faint outer glow on the CREATE button). Same `button.tsx` root as Cross-screen. Evidence: `programs--1440.png`, `programs--390.png`.
- **[P3]** `programs` — Two differently-styled status pills sit side-by-side on the card header: "Active" is a green-tinted outline pill (off-token green) while "Active Program" is an orange-tinted outline pill; the pair is redundant (both say the program is active) and mixes two tint styles on one row. Evidence: `programs--1440.png`.
- **[P3]** `programs` — Body copy reads "FourDay-Day Split" — a data/label glitch (double "Day") in the program meta line, visible on both breakpoints. Off-contract only as a polish/legibility nit, not a colour/spacing issue. Evidence: `programs--1440.png`, `programs--390.png`.
- Note (positive): the program card uses the correct `bg-card` + 1px border + `rounded-lg` surface with a tokened orange progress bar and consistent caption scale; mobile stacks the action row cleanly with no horizontal overflow.

### exercises

- **[P2]** `exercises` — The entire muscle-group filter chip set (Chest/Shoulders/Triceps/Lats/Biceps/Quadriceps/Hamstrings/Glutes/…) and every per-card muscle/equipment badge render in off-token named Tailwind colours (orange/violet/blue/red/green/pink), driven by `MUSCLE_GROUP_CONFIG`/`EQUIPMENT_CONFIG` already catalogued in Static findings; on this page it is the dominant visual, filling the sidebar and ~444 cards with non-token colour. Visible on both breakpoints. Evidence: `exercises--1440.png`, `exercises--390.png`.
- **[P3]** `exercises` — The grouped list renders 444 near-identical `bg-card` tiles in a very long fullPage scroll (13k px desktop, 36k px mobile) with only muscle-group section headers breaking it up; no sticky filter/section affordance means the sidebar filters scroll away immediately on mobile. Density/scannability note. Evidence: `exercises--390.png`.
- Note (positive): the Grouped/Grid/List view toggle (active = orange), search input, and card surfaces are on-contract (`bg-card` + border + `rounded-lg`), and the mobile layout stacks filters above the list with no horizontal overflow.

### hevy

- **[P1]** `hevy` — The CONNECT HEVY primary button renders as a dark muted-brown fill with grey-on-brown label text (see crop) — the label is barely legible against the fill and the button reads as disabled despite being the page's primary CTA. This is a genuine contrast failure, distinct from (and worse than) the washed-out-label-on-orange retro bevel flagged elsewhere. Same on mobile. Evidence: `hevy--1440.png`, `hevy--390.png`.
- **[P2]** `hevy` — CONNECT HEVY is ALL-CAPS Orbitron (retro leftover, `button.tsx` root). Evidence: `hevy--1440.png`.
- Note (positive): the "Connect your Hevy account to view synced routines" empty state is centred with a link glyph and two-line copy — the correct empty-state pattern; the integration card and API-key input are on-contract `bg-card`/tokened surfaces with no overflow at 390px.

### hevy-data

- **[P1]** `hevy-data` — The CONNECT HEVY button carries the same dark muted-brown fill / illegible grey label as on the Hevy page (same button, same failure). Evidence: `hevy-data--1440.png`, `hevy-data--390.png`.
- **[P2]** `hevy-data` — The "Connect your Hevy account to view workout data." empty state is a single left-aligned muted line in a tall `bg-card` panel — inconsistent with the centred glyph+copy empty states used on Hevy/History/Dashboard; reads as an unstyled placeholder rather than a designed empty state. Evidence: `hevy-data--1440.png`, `hevy-data--390.png`.
- Not verified: because the account is not Hevy-connected, no data table renders, so the table-overflow-at-390 check could not be exercised on this page; deferred to a connected-account pass.

### settings

- **[P2]** `settings` — Button-treatment inconsistency: the Settings actions ("Seed 4-Day Template (Weeks 1-17)", "Export Current Program") render in SENTENCE-CASE system font (Seed = flat orange, Export = flat dark secondary), whereas nav and every other page's buttons are ALL-CAPS Orbitron with the retro bevel. Settings is closer to the contract, but the app now shows two conflicting button styles — the same primitive should be used everywhere. Evidence: `settings--1440.png`, `settings--390.png`.
- **[P3]** `settings` (390px) — The fixed palm/island avatar overlaps the "Export Current Program" button, sitting on top of an interactive control (same decorative-overlay issue flagged across other screens). Evidence: `settings--390.png`.
- Note (positive): both Settings cards use correct `bg-card` + 1px border + `rounded-lg` surfaces with the proper heading/body/caption scale and clean `p-6` spacing; sentence-case flat buttons here are the on-contract button target.

### history

- **[P3]** `history` — The Block legend dots (Block 1 = blue, Block 2 = violet, Block 3 = pink) use the off-token literal hex from `lib/blockColors.ts` already catalogued as P3 in Static findings; contract sanctions that file for categorical identity, so this is polish only. Evidence: `history--1440.png`, `history--390.png`.
- **[P3]** `history` — "Export CSV" (secondary, icon + label) and the "Activity Calendar / Exercise Progress" tab toggle render in sentence-case system font, matching Settings but conflicting with the ALL-CAPS Orbitron buttons on Programs/Hevy/Simulator — same button-treatment inconsistency noted under Settings. Evidence: `history--1440.png`.
- Not reached: only the default "Activity Calendar" tab is captured, so the "Exercise Progress" charts (theme-token/axis-legibility per the contract's chart rules) could not be judged; deferred to a chart-rendering pass.
- Note (positive): the Activity Calendar card, the "Click on a workout day to see details" empty right-panel (centred glyph + copy), and today's tokened-orange highlighted cell are on-contract; the calendar grid wraps cleanly at 390px with no horizontal overflow.

### simulate

- **[P2]** `simulate` — Retro leftover: RUN SIMULATION and RUN PERSISTENT render ALL-CAPS Orbitron with the bevelled retro-orange fill (`button.tsx` root). Evidence: `simulate--1440.png`, `simulate--390.png`.
- Not reached: no simulation has been run, so the results table (the primary table-overflow-at-390 risk for this page) and any projection chart do not render; the table-scroll-within-container and chart-styling checks could not be exercised and are deferred to a post-run pass.
- Note (positive): the config card, "Persistent Run (dev)" panel, selects and number inputs are on-contract `bg-card`/tokened surfaces; the empty "Select a workout and click Run Simulation…" state is centred and clean, and the form stacks at 390px with no horizontal overflow.

### modal-weight-confirm

- Not reached: requires a completed session with a pending weight bump; deferred to Phase 2 verification.

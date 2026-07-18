# Frontend Audit Findings — Arcade Minimal

**Date started:** 2026-07-18
**Rubric:** `src/A2S.Web/src/AGENTS.md` (styling contract) + UX checklist in spec §3.
**Severity:** P1 breaks usability/legibility · P2 violates contract/inconsistent · P3 polish.

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

## Screen findings

_One subsection per route/flow, appended during the Playwright walkthrough (Task 6)._

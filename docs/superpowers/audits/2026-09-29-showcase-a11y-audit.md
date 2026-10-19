# Accessibility audit: Storybook showcase — 2026-09-29

**Branch:** `feature/storybook-showcase` · **Theme:** Arcade Minimal (orange on near-black)
**Tool:** axe-core via `@axe-core/playwright`, tags `wcag2a wcag2aa wcag21a wcag21aa`
**Harness:** `npm run a11y:stories` (`scripts/a11y-stories.mjs`): builds Storybook, opens every story in
Chromium, waits for its play function, network idle, fonts and chart animations, then runs axe on the whole
document (so portalled dialogs are included). Page stories run at 1440 and 390 wide, the rest at 1440.
**Scope:** 115 stories, 144 renders, all against the MSW mock data in `src/mocks`. This is the first audit
that covers the flows only reachable with data (session detail, simulator results, Hevy history modal) and
the real Clerk widget.

## Headline

| | critical | serious | total nodes | rules |
|---|---:|---:|---:|---|
| **Before** | 478 | 73 | **551** | aria-required-parent 460, nested-interactive 34, color-contrast 32, label 13, scrollable-region-focusable 7, select-name 5 |
| **After** | 0 | 35 | **35** | nested-interactive 34, color-contrast 1 |

Both remaining items are recorded below and deliberately left.

Before was measured after the theme pass (commits 1–3 of this branch), so contrast problems that pass fixed
(login page gold on navy, white-on-orange block labels, raw palette status colours) are not in this count.

## Fixed

### Contrast (32 → 1)

| Where | Failing pair | Ratio | Fix | After |
|---|---|---:|---|---:|
| Destructive buttons (Undo Workout, `Button variant="destructive"`) | white on `#ff3d3d` | 3.5 | `--color-destructive-foreground` → near-black `hsl(240 10% 8%)` | 5.3 |
| History calendar, today's cell | primary on `bg-primary/20` at 12px | 4.28 | Muted cell + primary ring, foreground text | 15.3 |
| History session detail, "(AMRAP)" suffix | primary at 70% opacity on tint | 3.14 | Drop the `opacity-70` | 5.0 |
| History exercise list, selected row caption | `primary-foreground/70` on primary | 3.77 | Full `primary-foreground` | 5.9 |
| History exercise detail, progression badge | neon blue on its 15% tint | 4.38 | Text mixed 60/40 with foreground (same approach as muscle-group badges); purple badge likewise | 6.8 (purple 5.7) |
| Setup wizard template cards, "+N more" | `muted-foreground/60` | 3.36 | Full `muted-foreground` | 7.3 |

### Structure

- **aria-required-parent (460):** calendar days used `role="gridcell"` with no grid/row parents. Days with a
  session are now `role="button"` (they open the detail panel); empty days carry no role.
  `features/history/WorkoutHistoryComponents.tsx`.
- **label / select-name (18):** exercise config dialog inputs had visible labels that weren't associated.
  Each input/select gets an `aria-label` matching its label ("Training Max", "Training Max unit", "Minimum
  reps", …). `features/workout/ExerciseSelectionV2/ExerciseConfigFields.tsx`.
- **scrollable-region-focusable (7):** scroll containers are now focusable named regions: simulator detail
  table and persistent-run log (`role="log"`), history weekly table, Hevy exercise session table. The setup
  wizard step strip (deferred in the July audit) got the same treatment in the theme commit.

### Found by eye while checking screenshots (not axe rules, same pass)

- Phone widths overflowed horizontally on the dashboard, program overview and session pages: the dashboard
  grid let wide children set the column width, the week/next-week headers didn't wrap, and the session's
  Undo/Complete row didn't wrap. All three wrap now; `scrollWidth` is 390 on every page story at 390.
- "Log AMRAP" overflowed its cell at 390px; phones show "Log".
- Session header sat outside the content column and its sticky progress bar slid under the navbar.
- History detail panel scrolled away from the day you clicked; it's sticky on large screens now.

### Clerk sign-in / sign-up

Rendered with the real Clerk widget (development instance). axe: **0 violations** at 1440 and 390 for both.
Measured ratios for the widget's text are in `features/auth/clerkAppearance.ts`: headings/labels 16.0:1,
subtitle/divider/footer 7.3:1, links 5.6:1, primary button label 5.9:1 (Clerk's default white on orange was
3.85:1), errors 5.1:1, input outline 3.8:1 against the card.

## Left as is

- **nested-interactive (34 nodes, `SelectedExercisesList` stories only).** dnd-kit puts `role="button"` and
  `tabIndex=0` on each sortable wrapper, and the card inside has Edit/Remove buttons. The fix is to move the
  sortable attributes onto the grip icon as a real drag-handle button. That changes how dragging works (the
  whole card stops being a drag target, including on touch), so it wants its own change and a manual test on
  a phone. Not reached by the page stories.
- **color-contrast (1 node, `SelectedExerciseCard` "Dragging" story).** The card is 50% transparent while it is
  being dragged. Transient state; left.
- Chart text is SVG; axe doesn't check it. Axis ticks use `--color-muted-foreground` (7.3:1 on the card) via
  `lib/chartTheme.ts`; the simulator's axis titles still use Recharts' default fill and weren't changed.

## Re-run

```
cd src/A2S.Web
npm run a11y:stories      # writes a11y-results.json, exits 1 on any violation
```

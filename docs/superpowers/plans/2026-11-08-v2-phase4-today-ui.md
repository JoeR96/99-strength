# v2 Phase 4 PR 6 — Today, Habits and Progress pages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Today screen answers "what do I do now?" in one glance and logging a bottle of water is one tap from app open; Habits shows counters, history and targets; Progress shows weight trend against checkpoints and weekly volume.

**Architecture:** Three new feature folders (`features/today`, `features/habits`, `features/progress`) built on typed API clients (`api/plans.ts`, `api/habits.ts`, `api/bodyWeight.ts`, `api/budget.ts`) and TanStack Query hooks with optimistic updates for habit taps. Every page has Storybook stories driven by MSW handlers (the existing pattern) and Vitest tests; phone-first at 390 px with 44 px+ targets. Offline queueing is PR 7 — here mutations are plain but structured so PR 7 can wrap them.

**Tech Stack:** React 19, TanStack Query 5, React Router 7, Tailwind 4, ShadCN primitives in `components/ui`, lucide-react, Recharts (already used), Storybook 10 + MSW, Vitest + Testing Library.

**Spec:** `docs/v2/brief.md` § 4 ("Today" paragraph, phone-first layout) and § 2 (habits, weekly goals, trend); `docs/v2/audit.md` § 2 for the friction being fixed; backend contracts below.

## Backend contracts (from Phase 3)

- `GET /api/v1/plans/active` → `TrainingPlanDto | 404`; `GET /api/v1/plans/today?date=` → `TodayDto { date, phase, sessions: TodaySessionDto[] { kind: 'Rest'|'Lift'|'Cardio'|'Mobility', label, status: 'Planned'|'Done'|'Missed'|'Paused'|'Rest', liftDay?, cardioProtocolId?, mobilityRoutineId?, completionId? }, week: WeeklyGoalsDto { weekStart, liftsPlanned, liftsDone, cardioPlanned, cardioDone, mobilityPlanned, mobilityDone, flexibleMealsUsed, flexibleMealsAllowed, alcoholOccasions, alcoholAllowed } }`; `GET /api/v1/plans/calendar?year=&month=`; `GET /api/v1/plans/weekly-goals?date=`; `POST /api/v1/plans/{id}/completions` `{ date, kind, liftDay?, cardioProtocolId?, mobilityRoutineId?, cardio?: { durationSeconds, avgHeartRate?, rpe?, distanceMeters?, notes? }, drillTicks?: boolean[] }`; `DELETE /api/v1/plans/{id}/completions/{completionId}`; `POST/DELETE /api/v1/plans/{id}/allowances` `{ date, kind: 'FlexibleMeal'|'Alcohol' }`.
- `GET /api/v1/habits/today?date=` → `HabitsTodayDto { date, habits: HabitTodayDto[] { id, name, type: 'Counter'|'Numeric'|'Checkbox', target, minimum, unit, value, metMinimum, metTarget, order, icon }, streak: { current, best, lastCountedDay, todayCounted, graceUsed } }`; `POST /api/v1/habits/{id}/logs/{date}/increment` `{ delta: 1|-1, clientTimestamp }`; `PUT /api/v1/habits/{id}/logs/{date}` `{ value, clientTimestamp }`; `GET /api/v1/habits/history?from=&to=` → `{ from, to, habits: HabitDto[], entries: { habitId, date, value, metMinimum, metTarget }[] }`; `GET /api/v1/habits?includeArchived=`; `POST /api/v1/habits`; `PUT /api/v1/habits/{id}`; `POST /api/v1/habits/{id}/archive|unarchive`.
- `PUT /api/v1/body-weight/{date}` `{ kg }`; `GET /api/v1/body-weight/trend?from=&to=` → `{ points: { date, kg, movingAverage7, ewma }[], pace?: { pace: 'OnPace'|'Behind'|'Ahead'|'NoTarget', targetKgToday, trendKg, deltaKg } }`.
- `GET /api/v1/workouts/{id}/days/{day}/budget?week=` → `{ hardSetsThisWeek: Record<string, number>, volumeWarnings: { bucket, sets, cap, message }[], time: { totalMinutes, warmUpMinutes, perExercise, overBudget, budgetMinutes } }`.
- Dates are `yyyy-MM-dd` strings; the client's "today" is computed in Europe/London with `Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/London' })` in `lib/localDate.ts` (`todayLocal()`, `formatLocal(date)`), never `new Date().toISOString().slice(0,10)`.

## Global Constraints

- 390 px first: no horizontal scroll, every tap target ≥ 44 px, primary habit buttons 64 px, bottom tab bar (PR 5) stays visible.
- Optimistic updates: a habit tap updates the cache immediately and rolls back on error with a toast.
- `clientTimestamp` = `new Date().toISOString()` at tap time (PR 7 replays it unchanged).
- Dark theme tokens only (`bg-card`, `text-muted-foreground`, …); icons from lucide-react; no new UI libraries.
- Each new page: a `*.stories.tsx` with `Default`, `Empty`, `Loading`, `Error` and the states listed per task, driven by MSW handlers in `src/mocks/handlers/*.ts`; axe clean (Storybook a11y addon).
- Vitest for hooks and tap logic; Playwright screenshots via the Storybook harness at 390 px for the audit-after.

## Review Focus

1. **Water tap at 23:59 then 00:01 local** writes to two different dates: the tap uses `todayLocal()` at tap time, and the Today query key includes the date so the page rolls over without a reload. Pinned in `useHabitsToday` tests with a fake clock.
2. **Double-tap `+`** sends two increments and shows 2, never 1 or 3 after reconciliation (server value wins). Pinned in `HabitCounter` tests.
3. **No active plan** (first run): Today shows the onboarding card (Phase 7 fills it; here a "Start a plan" link to `/setup`) and still shows habits and weigh-in. Pinned in the `NoPlan` story and a test.
4. **Paused phase**: Today shows "Paused — holiday until 3 Jan" and no Start button; weekly rings exclude paused sessions. Pinned in the `Paused` story.
5. **Weigh-in already logged today**: the field shows the value with "Edit", not an empty input, and saving the same value is a no-op. Pinned in `WeighInField` tests.

---

### Task 6.1: Local dates and typed API clients
Files: `src/lib/localDate.ts` (+test), `src/api/plans.ts`, `src/api/habits.ts`, `src/api/bodyWeight.ts`, `src/api/budget.ts`, `src/types/plan.ts`, `src/types/habits.ts`; MSW handlers `src/mocks/handlers/plans.ts`, `habits.ts`, `bodyWeight.ts` with fixtures matching the brief's seed (Mon Upper A… Sun Rest; water 2/6, walks 1/2, weigh-in 94.6, streak 5).
Tests: `localDate` across the 25 Oct 2026 and 28 Mar 2027 changes with a mocked `Date`.

### Task 6.2: Query hooks
Files: `src/hooks/usePlan.ts` (`useToday(date)`, `useWeeklyGoals`, `useCompleteSession`, `useUndoCompletion`, `useAllowance`), `src/hooks/useHabits.ts` (`useHabitsToday(date)`, `useIncrementHabit` with optimistic cache update and rollback, `useSetHabitValue`, `useHabitHistory`), `src/hooks/useBodyWeight.ts` (`useWeightTrend`, `useRecordWeight`), `src/hooks/useDayBudget.ts`.
Tests: optimistic increment then server reconcile; rollback on 500 shows toast; query keys include the date.

### Task 6.3: Today page
Files: `src/features/today/TodayPage.tsx`, `SessionCard.tsx` (kind icon, label, status chip, **Start** → `/workout/session/{liftDay}` for lifts, **Open in Hevy** `hevy://` link with web fallback when a routine link exists (Phase 6 supplies it; until then hidden), **Mark done** for cardio/mobility opening `CardioLogSheet`/`MobilityChecklist`), `HabitCounter.tsx` (64 px `+`, `−` behind long-press ≥ 500 ms or a small secondary button, value/target, minimum tick), `WeighInField.tsx` (`inputMode="decimal"`, kg, Save/Edit), `WeeklyRings.tsx` (lifts, cardio, mobility, flexible meal, alcohol; paused excluded), `StreakChip.tsx` (current, grace used = amber), `PhaseBanner.tsx` (Cut/Maintenance/Pause with end date). Route `/today` becomes the real page and `/dashboard` redirects to it; tabs config updated.
Stories: Default (Mon Upper A planned), Done, Rest day, Cardio day (4×4 + Mobility B), Paused, NoPlan, Offline-pending placeholder (badge slot for PR 7).
Tests: one tap from mount increments water; long-press decrements; rings math; paused hides Start.

### Task 6.4: Habits page
Files: `src/features/habits/HabitsPage.tsx` (replaces the placeholder), `HabitList.tsx` (counters/checkbox/numeric rows, drag order later), `HabitHeatmap.tsx` (last 12 weeks, day met-minimum/met-target/none), `HabitEditSheet.tsx` (name, type, target, minimum, unit, icon; archive), `NewHabitButton.tsx`.
Stories: Default with 5 habits and 30 days of history, Empty, Editing.
Tests: minimum > target shows inline error; archive removes from today list.

### Task 6.5: Progress page
Files: `src/features/progress/ProgressPage.tsx` (replaces Progress tab target `/history` → `/progress`; history keeps a link), `WeightTrendChart.tsx` (Recharts: daily kg dots, EWMA line, checkpoint markers and straight target line, pace label On pace/Behind/Ahead with delta), `VolumeThisWeek.tsx` (bars per bucket vs cap from `GET .../budget`, warnings), `CheckpointsList.tsx`.
Stories: OnPace, Behind, NoTarget (no plan), FewPoints (< 7 days → MA7 absent).
Tests: pace label text and colour; chart renders with 0 points without crashing.

### Task 6.6: Session page fixes (audit 2.3)
In `useWorkoutSession.ts` and `workoutReconciliationHandlers.ts`: undo navigates to `/workout` (not `/workout/day/…`); the "skip progression" decision is sent in the payload; missing-exercise "delete" and "skip" diverge; discrepancy "update" respects the unit; resumed sessions keep the original `workoutStartTime`; the Hevy title is built from the pre-refetch week. Partial completion allowed (uncompleted exercises omitted, not zero-filled); deload banner; planned sets come from the API week plan rather than the deprecated local calculators.
Tests: a Vitest for each bug (six), plus the first tests for `useWorkoutSession` (render hook with mocked API).

### Task 6.7: Audit-after captures
Extend `scripts/capture-audit-screens.mjs` (from PR #25's Storybook harness, now in the repo) to the new stories; save to `docs/v2/screens-after/`; record tap counts for "log a bottle of water" (target 1) in `docs/v2/audit-after.md` draft.

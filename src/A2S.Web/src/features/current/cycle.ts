/**
 * Pure helpers for the Current workout page: one-line set summaries, an estimated session time and
 * the split of the cycle into "up next" and "next week". Nothing here looks at the calendar.
 */

import type { CycleDto, CycleSessionDto, PlannedExerciseDto, WeekPlanDto } from '@/api/workouts';
import { cardioBlockLine } from '@/features/gym/gymCardio';

function kg(value: number): string {
  return `${Number.isInteger(value) ? value : value.toFixed(1)} kg`;
}

/** "4 × 5, 1 × 10+ · 80 kg", "3 × 8–12 · 20 kg", "5 × 5 · set TM". */
export function setSummary(exercise: PlannedExerciseDto): string {
  if (exercise.cardioBlock) return cardioBlockLine(exercise.cardioBlock);
  const sets = exercise.plannedSets;
  if (sets.length === 0) return 'No sets this week';
  const groups: { count: number; reps: number; amrap: boolean }[] = [];
  for (const set of sets) {
    const last = groups[groups.length - 1];
    if (last && last.reps === set.targetReps && last.amrap === set.isAmrap) last.count += 1;
    else groups.push({ count: 1, reps: set.targetReps, amrap: set.isAmrap });
  }
  const range =
    exercise.metadata.repRangeMinimum && exercise.metadata.repRangeMaximum && !sets.some((s) => s.isAmrap)
      ? `${sets.length} × ${exercise.metadata.repRangeMinimum}–${exercise.metadata.repRangeMaximum}`
      : null;
  const reps = range ?? groups.map((g) => `${g.count} × ${g.reps}${g.amrap ? '+' : ''}`).join(', ');
  if (exercise.metadata.isTrainingMaxPending) return `${reps} · set TM`;
  if (exercise.metadata.isWeightPending) return `${reps} · weight to set`;
  const weights = sets.map((s) => s.weightKg).filter((w) => w > 0);
  if (weights.length === 0) return reps;
  const min = Math.min(...weights);
  const max = Math.max(...weights);
  return `${reps} · ${min === max ? kg(max) : `${kg(min).replace(' kg', '')}–${kg(max)}`}`;
}

/** Work + rest per set (rest from the plan; about 40 s a set), plus cardio blocks, the finisher and the warm-up. Whole minutes. */
export function estimateMinutes(plan: WeekPlanDto, warmUpMinutes = 0): number {
  const lifting = plan.exercises.reduce((total, e) => total + e.plannedSets.length * ((e.restSeconds || 90) + 40), 0);
  const cardioBlocks = plan.exercises.reduce((total, e) => total + (e.cardioBlock?.totalMinutes ?? 0), 0);
  return Math.round(lifting / 60 + cardioBlocks + (plan.cardio?.minutes ?? 0) + warmUpMinutes);
}

/** True when any lift of the session has no training max yet. */
export function needsTrainingMax(plan: WeekPlanDto): boolean {
  return plan.exercises.some((e) => e.metadata.isTrainingMaxPending);
}

export interface CycleSplit {
  current: CycleSessionDto | null;
  /** The rest of the current week after the current session. */
  thisWeek: CycleSessionDto[];
  /** Every session of the next week. */
  nextWeek: CycleSessionDto[];
}

export function splitCycle(cycle: CycleDto): CycleSplit {
  const current = cycle.sessions.find((s) => s.isCurrent) ?? cycle.sessions[0] ?? null;
  const rest = cycle.sessions.filter((s) => s !== current);
  return {
    current,
    thisWeek: rest.filter((s) => s.week === cycle.currentWeek),
    nextWeek: rest.filter((s) => s.week === cycle.currentWeek + 1),
  };
}

/** The read-only gym preview for a cycle position. */
export function previewHref(session: Pick<CycleSessionDto, 'week' | 'day'>): string {
  return `/gym/${session.day}?preview=1&week=${session.week}`;
}

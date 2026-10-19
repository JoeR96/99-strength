/**
 * Numbers the history page derives from GET /workouts/history. Pure functions,
 * so the calendar, the session panel and the one-rep-max chart agree with each other.
 */
import type {
  CompletedSetDto,
  ExerciseHistoryDto,
  WorkoutActivityDto,
  WorkoutHistoryDto,
} from './historyTypes';

export const unitLabel = (unit: string) => (unit === 'Pounds' ? 'lbs' : 'kg');

/** Epley estimate: weight × (1 + reps / 30). A single rep is the weight itself. */
export function estimateOneRepMax(weight: number, reps: number): number {
  if (reps <= 0) return 0;
  return reps === 1 ? weight : weight * (1 + reps / 30);
}

export interface SessionTotals {
  exercises: number;
  sets: number;
  /** Sum of weight × reps over every set */
  volume: number;
}

export function sessionTotals(activity: WorkoutActivityDto): SessionTotals {
  let sets = 0;
  let volume = 0;
  for (const performance of activity.performances) {
    for (const set of performance.completedSets) {
      sets += 1;
      volume += set.weight * set.actualReps;
    }
  }
  return { exercises: activity.performances.length, sets, volume };
}

export interface TopSet {
  exerciseId: string;
  weight: number;
  reps: number;
  unit: string;
  wasAmrap: boolean;
  estimate: number;
}

const toTopSet = (exerciseId: string, set: CompletedSetDto): TopSet => ({
  exerciseId,
  weight: set.weight,
  reps: set.actualReps,
  unit: unitLabel(set.weightUnit),
  wasAmrap: set.wasAmrap,
  estimate: estimateOneRepMax(set.weight, set.actualReps),
});

/** The session's strongest set: the one with the highest estimated one-rep max. */
export function topSet(activity: WorkoutActivityDto): TopSet | null {
  let best: TopSet | null = null;
  for (const performance of activity.performances) {
    for (const set of performance.completedSets) {
      const candidate = toTopSet(performance.exerciseId, set);
      if (!best || candidate.estimate > best.estimate) best = candidate;
    }
  }
  return best;
}

export interface OneRepMaxPoint {
  weekNumber: number;
  blockNumber: number;
  /** Axis label, e.g. "W8" */
  label: string;
  /** Estimated one-rep max, rounded to one decimal place */
  estimate: number;
  /** The AMRAP set the estimate comes from */
  weight: number;
  reps: number;
}

/**
 * One point per week, from that week's AMRAP (as many reps as possible) set.
 * Deload weeks have no AMRAP set and are skipped, so they do not show as a drop in strength.
 */
export function oneRepMaxSeries(exercise: ExerciseHistoryDto): OneRepMaxPoint[] {
  const points: OneRepMaxPoint[] = [];
  for (const week of exercise.weeklyHistory) {
    let best: CompletedSetDto | null = null;
    for (const set of week.sets) {
      if (!set.wasAmrap || set.actualReps <= 0) continue;
      if (!best || estimateOneRepMax(set.weight, set.actualReps) > estimateOneRepMax(best.weight, best.actualReps)) {
        best = set;
      }
    }
    if (!best) continue;
    points.push({
      weekNumber: week.weekNumber,
      blockNumber: week.blockNumber,
      label: `W${week.weekNumber}`,
      estimate: Math.round(estimateOneRepMax(best.weight, best.actualReps) * 10) / 10,
      weight: best.weight,
      reps: best.actualReps,
    });
  }
  return points;
}

export interface ProgramTotals {
  sessions: number;
  /** Sessions the whole program asks for: weeks × days per week */
  planned: number;
  sets: number;
  volume: number;
}

export function programTotals(history: WorkoutHistoryDto): ProgramTotals {
  let sets = 0;
  let volume = 0;
  for (const activity of history.completedActivities) {
    const totals = sessionTotals(activity);
    sets += totals.sets;
    volume += totals.volume;
  }
  return {
    sessions: history.completedActivities.length,
    planned: history.totalWeeks * history.daysPerWeek,
    sets,
    volume,
  };
}

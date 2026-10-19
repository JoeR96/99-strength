/**
 * Personal records from workout history. "Best" is the AMRAP set with the highest
 * Epley estimated 1RM: weight × (1 + reps / 30).
 */
import type { ExerciseHistoryDto } from '@/types/workout';

export interface PersonalRecord {
  exerciseId: string;
  name: string;
  weight: number;
  reps: number;
  unit: string;
  weekNumber: number;
  estimatedOneRepMax: number;
}

function epley(weight: number, reps: number): number {
  return reps <= 1 ? weight : weight * (1 + reps / 30);
}

/** Best AMRAP set per Linear exercise, highest e1RM first. Exported for tests. */
export function findPersonalRecords(histories: ExerciseHistoryDto[]): PersonalRecord[] {
  const records: PersonalRecord[] = [];
  for (const exercise of histories) {
    if (exercise.progressionType !== 'Linear') continue;
    let best: PersonalRecord | null = null;
    for (const week of exercise.weeklyHistory) {
      for (const set of week.sets) {
        if (!set.wasAmrap || set.actualReps <= 0) continue;
        const e1rm = epley(set.weight, set.actualReps);
        if (!best || e1rm > best.estimatedOneRepMax) {
          best = {
            exerciseId: exercise.exerciseId,
            name: exercise.name,
            weight: set.weight,
            reps: set.actualReps,
            unit: exercise.weightUnit === 'Pounds' ? 'lbs' : 'kg',
            weekNumber: week.weekNumber,
            estimatedOneRepMax: e1rm,
          };
        }
      }
    }
    if (best) records.push(best);
  }
  return records.sort((a, b) => b.estimatedOneRepMax - a.estimatedOneRepMax);
}

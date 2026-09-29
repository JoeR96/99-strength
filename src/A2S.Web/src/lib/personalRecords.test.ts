import { describe, it, expect } from 'vitest';
import { findPersonalRecords } from './personalRecords';
import type { ExerciseHistoryDto } from '@/types/workout';

function history(
  name: string,
  progressionType: string,
  weeks: { week: number; sets: [number, number, boolean][] }[]
): ExerciseHistoryDto {
  return {
    exerciseId: name,
    name,
    progressionType,
    assignedDay: 1,
    category: 'MainLift',
    equipment: 'Barbell',
    currentWeight: 0,
    weightUnit: 'Kilograms',
    currentSets: 5,
    targetSets: 5,
    weeklyHistory: weeks.map(({ week, sets }) => ({
      weekNumber: week,
      blockNumber: 1,
      isDeloadWeek: false,
      totalVolume: 0,
      averageWeight: 0,
      totalReps: 0,
      setsCompleted: sets.length,
      sets: sets.map(([weight, actualReps, wasAmrap], i) => ({
        setNumber: i + 1,
        weight,
        weightUnit: 'Kilograms',
        actualReps,
        wasAmrap,
      })),
    })),
  };
}

describe('findPersonalRecords', () => {
  it('picks the AMRAP set with the best estimated 1RM per linear lift', () => {
    const records = findPersonalRecords([
      history('Squat', 'Linear', [
        { week: 1, sets: [[100, 5, false], [100, 10, true]] },
        { week: 2, sets: [[105, 5, false], [105, 8, true]] },
      ]),
    ]);

    expect(records).toHaveLength(1);
    expect(records[0]).toMatchObject({ name: 'Squat', weight: 100, reps: 10, weekNumber: 1 });
    expect(Math.round(records[0].estimatedOneRepMax)).toBe(133);
  });

  it('ignores non-AMRAP sets and non-linear exercises, and sorts by e1RM', () => {
    const records = findPersonalRecords([
      history('Curl', 'RepsPerSet', [{ week: 1, sets: [[20, 12, true]] }]),
      history('Bench', 'Linear', [{ week: 1, sets: [[200, 1, false], [80, 8, true]] }]),
      history('Deadlift', 'Linear', [{ week: 1, sets: [[160, 6, true]] }]),
    ]);

    expect(records.map((r) => r.name)).toEqual(['Deadlift', 'Bench']);
  });
});

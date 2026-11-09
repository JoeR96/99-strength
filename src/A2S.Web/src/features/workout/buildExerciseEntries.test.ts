import { describe, it, expect } from 'vitest';
import { buildExerciseEntries } from './buildExerciseEntries';
import type { WeekPlanDto } from '@/api/workouts';
import type { ExerciseDto } from '@/types/workout';

const bench = {
  id: 'ex-1',
  name: 'Bench Press',
  assignedDay: 1,
  orderInDay: 1,
  progression: { type: 'Linear', trainingMax: { value: 200, unit: 2 }, useAmrap: true, baseSetsPerExercise: 5 },
} as unknown as ExerciseDto;

const plan: WeekPlanDto = {
  workoutId: 'w-1',
  workoutName: 'Test',
  weekNumber: 7,
  dayNumber: 1,
  blockNumber: 1,
  isDeloadWeek: true,
  intensityPercentage: 60,
  exercises: [
    {
      exerciseId: 'ex-1',
      name: 'Bench Press',
      category: 'MainLift',
      equipment: 'Barbell',
      orderInDay: 1,
      hevyExerciseTemplateId: 'bench',
      progressionType: 'Linear',
      plannedSets: [
        { setNumber: 1, weightKg: 54.4, weightLbs: 120, originalUnit: 'Pounds', targetReps: 5, isAmrap: false },
        { setNumber: 2, weightKg: 54.4, weightLbs: 120, originalUnit: 'Pounds', targetReps: 5, isAmrap: false },
      ],
      metadata: {},
    },
  ],
};

describe('buildExerciseEntries', () => {
  it('uses the plan\'s sets in the exercise\'s own unit', () => {
    const [entry] = buildExerciseEntries([bench], 7, plan);
    expect(entry.weightUnit).toBe('lbs');
    expect(entry.sets.map((s) => s.weight)).toEqual([120, 120]);
    expect(entry.sets.map((s) => s.reps)).toEqual([5, 5]);
    expect(entry.isAmrapExercise).toBe(false);
    expect(entry.targetSets).toBe(2);
  });

  it('falls back to the local calculators without a plan', () => {
    const [entry] = buildExerciseEntries([bench], 2, null);
    expect(entry.weightUnit).toBe('lbs');
    expect(entry.sets.length).toBeGreaterThan(0);
    expect(entry.sets[entry.sets.length - 1].isAmrap).toBe(true);
  });
});

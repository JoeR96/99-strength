import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createMissingExerciseHandler, toExerciseUnit, type ReconciliationDeps } from './workoutReconciliationHandlers';
import type { ExerciseEntry, MissingExercise } from './workoutSessionTypes';
import type { WorkoutDto } from '@/types/workout';

vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }),
}));

vi.mock('@/api/workouts', () => ({
  workoutsApi: { updateWorkingWeight: vi.fn() },
}));

function entry(id: string): ExerciseEntry {
  return {
    exercise: { id, name: id } as ExerciseEntry['exercise'],
    sets: [{ setNumber: 1, weight: 50, reps: 10, isAmrap: false, completed: false }],
    targetSets: 1,
    targetReps: 10,
    targetWeight: 50,
    weightUnit: 'kg',
    isAmrapExercise: false,
  };
}

function makeDeps() {
  let entries: ExerciseEntry[] = [entry('ex-1'), entry('ex-2')];
  const deps: ReconciliationDeps = {
    workout: { id: 'w-1', currentWeek: 2 } as WorkoutDto,
    exerciseEntries: entries,
    setExerciseEntries: vi.fn((update) => {
      entries = typeof update === 'function' ? update(entries) : update;
    }),
    setTemporarySubstitutions: vi.fn(),
    setIsPrefilled: vi.fn(),
    substituteExercise: { mutateAsync: vi.fn() },
    updateExercisesMutation: { mutateAsync: vi.fn() },
    removeExerciseMutation: { mutateAsync: vi.fn().mockResolvedValue(undefined) },
    refetch: vi.fn().mockResolvedValue({ data: null }),
    convertWeightFromKg: (kg) => kg,
  };
  return { deps, entries: () => entries };
}

const missing: MissingExercise = { exerciseId: 'ex-2', exerciseName: 'ex-2', prescribedSets: 3, prescribedReps: 10, prescribedWeight: 50 };

describe('toExerciseUnit', () => {
  it('keeps kilograms as kilograms', () => {
    expect(toExerciseUnit(82.5, 'kg')).toEqual({ weight: 82.5, unit: 1, label: 'kg' });
  });

  it('converts Hevy kilograms to pounds for a pounds exercise', () => {
    expect(toExerciseUnit(90.7, 'lbs')).toEqual({ weight: 200, unit: 2, label: 'lbs' });
  });
});

describe('createMissingExerciseHandler', () => {
  beforeEach(() => vi.clearAllMocks());

  it('"skip" keeps the exercise in the programme with no sets this session', async () => {
    const { deps, entries } = makeDeps();
    await createMissingExerciseHandler(deps)(missing, 'skip');
    expect(deps.removeExerciseMutation.mutateAsync).not.toHaveBeenCalled();
    const skipped = entries().find((e) => e.exercise.id === 'ex-2');
    expect(skipped).toBeDefined();
    expect(skipped?.sets).toEqual([]);
    expect(skipped?.skipProgression).toBe(true);
  });

  it('"delete" removes the exercise from the programme and the session', async () => {
    const { deps, entries } = makeDeps();
    await createMissingExerciseHandler(deps)(missing, 'delete');
    expect(deps.removeExerciseMutation.mutateAsync).toHaveBeenCalledWith({ workoutId: 'w-1', exerciseId: 'ex-2' });
    expect(entries().map((e) => e.exercise.id)).toEqual(['ex-1']);
    expect(deps.refetch).toHaveBeenCalled();
  });
});

/**
 * First tests for the session hook. Each pins one of the audit §2.3 bugs plus
 * the week-plan and partial-completion behaviour.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { useEffect, type ReactNode } from 'react';
import { workoutsApi, type WeekPlanDto } from '@/api/workouts';
import { getWeekParameters, roundToGymIncrement } from '@/utils/weekParameters';
import type { WorkoutDto, ExerciseDto } from '@/types/workout';
import { useWorkoutSession } from './useWorkoutSession';
import type { SavedWorkoutProgress, WeightDiscrepancy } from './workoutSessionTypes';

vi.mock('@/api/workouts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/workouts')>();
  return {
    ...actual,
    workoutsApi: {
      getCurrentWorkout: vi.fn(),
      getWeekPlan: vi.fn(),
      completeDay: vi.fn(),
      undoLastCompletion: vi.fn(),
      updateExercises: vi.fn(),
      removeExercise: vi.fn(),
      substituteExercise: vi.fn(),
      updateWorkingWeight: vi.fn(),
      confirmStartingWeight: vi.fn(),
    },
  };
});

vi.mock('@/contexts/HevyContext', () => ({
  useHevy: () => ({ isConfigured: false, isValid: false }),
}));

vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }),
}));

const exercises: ExerciseDto[] = [
  {
    id: 'ex-1',
    name: 'Bench Press',
    category: 1,
    equipment: 0,
    assignedDay: 1,
    orderInDay: 1,
    hevyExerciseTemplateId: 'bench',
    progression: { type: 'Linear', trainingMax: { value: 100, unit: 1 }, useAmrap: true, baseSetsPerExercise: 5 },
  },
  {
    id: 'ex-2',
    name: 'Incline Press (lbs)',
    category: 2,
    equipment: 1,
    assignedDay: 1,
    orderInDay: 2,
    hevyExerciseTemplateId: 'incline',
    progression: { type: 'Linear', trainingMax: { value: 200, unit: 2 }, useAmrap: false, baseSetsPerExercise: 3 },
  },
  {
    id: 'ex-3',
    name: 'Face Pull',
    category: 3,
    equipment: 2,
    assignedDay: 1,
    orderInDay: 3,
    hevyExerciseTemplateId: 'facepull',
    progression: {
      type: 'RepsPerSet',
      repRange: { minimum: 10, maximum: 15 },
      startingSets: 3,
      currentSetCount: 3,
      targetSets: 5,
      currentWeight: 20,
      weightUnit: 'Kilograms',
      isUnilateral: false,
      isWeightPending: false,
    },
  },
] as unknown as ExerciseDto[];

const workout: WorkoutDto = {
  id: 'w-1',
  name: 'Cut to 85 — A2S2',
  variant: 4,
  status: 2,
  currentWeek: 2,
  currentBlock: 1,
  currentDay: 1,
  daysPerWeek: 4,
  completedDaysInCurrentWeek: [],
  isWeekComplete: false,
  totalWeeks: 21,
  blockSequence: [1, 2, 3],
  startDate: '2026-11-09',
  createdAt: '2026-11-08',
  startedAt: '2026-11-09',
  exerciseCount: 3,
  exercises,
};

/** Records the router's pathname so a test can assert where a handler navigated. */
const pathLog = { current: '' };
function LocationProbe() {
  const { pathname } = useLocation();
  useEffect(() => {
    pathLog.current = pathname;
  }, [pathname]);
  return null;
}

function createWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={['/workout/session/1']}>
          <LocationProbe />
          <Routes>
            <Route path="/workout/session/:day" element={<>{children}</>} />
            <Route path="/workout" element={<div data-testid="plan" />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
  };
}

async function renderSession() {
  const utils = renderHook(() => useWorkoutSession(), { wrapper: createWrapper() });
  await waitFor(() => expect(utils.result.current.exerciseEntries.length).toBe(3));
  return utils;
}

const benchDiscrepancy: WeightDiscrepancy = {
  exerciseId: 'ex-1',
  exerciseName: 'Bench Press',
  prescribedWeight: 80,
  actualWeights: [82.5],
  hasVaryingWeights: false,
  sets: [],
  progressionType: 'Linear',
};

describe('useWorkoutSession', () => {
  beforeEach(() => {
    pathLog.current = '';
    vi.mocked(workoutsApi.getCurrentWorkout).mockResolvedValue(workout);
    vi.mocked(workoutsApi.getWeekPlan).mockRejectedValue(new Error('plan unavailable'));
    vi.mocked(workoutsApi.completeDay).mockResolvedValue({
      workoutId: 'w-1',
      day: 1,
      weekNumber: 2,
      blockNumber: 1,
      exercisesCompleted: 1,
      progressionChanges: [],
      newCurrentWeek: 2,
      newCurrentDay: 2,
      weekProgressed: false,
      programComplete: false,
      isDeloadWeek: false,
      exercisesPendingWeightConfirmation: [],
      nextSessionExercises: [],
    });
    vi.mocked(workoutsApi.undoLastCompletion).mockResolvedValue({ day: 1, weekNumber: 2, weekRolledBack: false, newCurrentWeek: 2, newCurrentDay: 1 });
    vi.mocked(workoutsApi.updateExercises).mockResolvedValue({} as never);
    vi.mocked(workoutsApi.removeExercise).mockResolvedValue(undefined);
  });

  it('builds planned sets from the API week plan and exposes the deload flag', async () => {
    const plan: WeekPlanDto = {
      workoutId: 'w-1',
      workoutName: workout.name,
      weekNumber: 2,
      dayNumber: 1,
      blockNumber: 1,
      isDeloadWeek: true,
      intensityPercentage: 70,
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
            { setNumber: 1, weightKg: 77.5, weightLbs: 170.9, originalUnit: 'Kilograms', targetReps: 8, isAmrap: false },
            { setNumber: 2, weightKg: 77.5, weightLbs: 170.9, originalUnit: 'Kilograms', targetReps: 8, isAmrap: true },
          ],
          metadata: {},
        },
      ],
    };
    vi.mocked(workoutsApi.getWeekPlan).mockResolvedValue(plan);
    const { result } = await renderSession();
    expect(workoutsApi.getWeekPlan).toHaveBeenCalledWith('w-1', 2, 1);
    const bench = result.current.exerciseEntries[0];
    expect(bench.sets).toHaveLength(2);
    expect(bench.sets[0]).toMatchObject({ weight: 77.5, reps: 8, isAmrap: false });
    expect(bench.sets[1].isAmrap).toBe(true);
    expect(result.current.isDeloadWeek).toBe(true);
    // Exercises the plan does not list still get the local fallback.
    expect(result.current.exerciseEntries[2].sets).toHaveLength(3);
  });

  it('falls back to the local calculators when the week plan fails', async () => {
    const { result } = await renderSession();
    const week = getWeekParameters(2);
    expect(result.current.exerciseEntries[0].sets).toHaveLength(week.sets);
    expect(result.current.exerciseEntries[0].sets[0].weight).toBe(roundToGymIncrement(100 * week.intensity));
    expect(result.current.exerciseEntries[1].weightUnit).toBe('lbs');
    expect(result.current.isDeloadWeek).toBe(false);
  });

  it('undo navigates to /workout, not /workout/day/…', async () => {
    const { result } = await renderSession();
    await act(async () => {
      await result.current.handleUndoCompletion();
    });
    expect(workoutsApi.undoLastCompletion).toHaveBeenCalledWith('w-1');
    await waitFor(() => expect(pathLog.current).toBe('/workout'));
  });

  it('sends the "skip progression" decision in the completion payload', async () => {
    const { result } = await renderSession();
    await act(async () => {
      await result.current.handleApplyWeightDiscrepancy(benchDiscrepancy, 82.5, 'skip');
    });
    act(() => result.current.handleSetComplete(0, 0));
    await act(async () => {
      await result.current.handleCompleteWorkout();
    });
    expect(workoutsApi.completeDay).toHaveBeenCalledTimes(1);
    const [, , request] = vi.mocked(workoutsApi.completeDay).mock.calls[0];
    expect(request.performances.find((p) => p.exerciseId === 'ex-1')).toMatchObject({ wasTemporarySubstitution: true });
  });

  it('allows a partial session: one logged set completes, unlogged exercises are omitted', async () => {
    const { result } = await renderSession();
    expect(result.current.canComplete).toBe(false);
    act(() => result.current.handleSetComplete(0, 0));
    expect(result.current.canComplete).toBe(true);
    expect(result.current.allSetsCompleted).toBe(false);
    await act(async () => {
      await result.current.handleCompleteWorkout();
    });
    const [, , request] = vi.mocked(workoutsApi.completeDay).mock.calls[0];
    expect(request.performances).toHaveLength(1);
    expect(request.performances[0]).toMatchObject({ exerciseId: 'ex-1', wasTemporarySubstitution: false });
    expect(request.performances[0].completedSets).toHaveLength(1);
  });

  it('the discrepancy "update" writes the training max in the exercise\'s unit (pounds)', async () => {
    const { result } = await renderSession();
    const inclineKg = 90.7; // what Hevy reports for 200 lbs
    await act(async () => {
      await result.current.handleApplyWeightDiscrepancy({ ...benchDiscrepancy, exerciseId: 'ex-2', exerciseName: 'Incline Press (lbs)' }, inclineKg, 'update');
    });
    expect(workoutsApi.updateExercises).toHaveBeenCalledTimes(1);
    const [, request] = vi.mocked(workoutsApi.updateExercises).mock.calls[0];
    const expectedTm = roundToGymIncrement(200 / getWeekParameters(2).intensity, 'lbs');
    expect(request.updates[0]).toMatchObject({ exerciseId: 'ex-2', trainingMaxUnit: 2, trainingMaxValue: expectedTm });
  });

  it('a resumed session keeps the start time it was first opened with', async () => {
    const saved: SavedWorkoutProgress = {
      workoutId: 'w-1',
      dayNumber: 1,
      weekNumber: 2,
      savedAt: '2026-11-09T07:10:00.000Z',
      startedAt: '2026-11-09T06:30:00.000Z',
      exercises: [{ exerciseId: 'ex-1', sets: [{ setNumber: 1, weight: 80, reps: 8, isAmrap: false, completed: true }] }],
    };
    vi.mocked(window.localStorage.getItem).mockReturnValue(JSON.stringify(saved));
    const { result } = await renderSession();
    await waitFor(() => expect(result.current.showRecoveryModal).toBe(true));
    act(() => result.current.handleResumeProgress());
    expect(result.current.workoutStartTime.current.toISOString()).toBe('2026-11-09T06:30:00.000Z');
    expect(result.current.exerciseEntries[0].sets[0]).toMatchObject({ weight: 80, completed: true });
  });
});

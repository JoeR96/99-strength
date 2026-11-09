/**
 * Pins defect 9 of the live walkthrough: saving an exercise change in a session no longer rebuilds
 * a Hevy routine from the browser. With a key stored it says where the push now lives.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import toast from 'react-hot-toast';
import { hevyApi } from '@/services/hevyApi';
import {
  PUSH_AGAIN_HINT,
  createChangeProgressionHandler,
  createSaveExerciseConfigHandler,
  type WorkoutConfigDeps,
} from './workoutConfigHandlers';
import type { WorkoutDto } from '@/types/workout';

vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn(), loading: vi.fn() }) }));
vi.mock('@/services/hevyApi', () => ({
  hevyApi: {
    isConfigured: vi.fn(),
    getAllRoutines: vi.fn(),
    deleteRoutine: vi.fn(),
  },
}));

const workout = {
  id: 'w-1',
  name: 'Cut',
  currentWeek: 2,
  exercises: [{ id: 'ex-1', name: 'Lateral Raise (Cable)', progression: { type: 'RepsPerSet' } }],
} as unknown as WorkoutDto;

function makeDeps(): WorkoutConfigDeps {
  return {
    workout,
    dayNumber: 1,
    refetch: vi.fn().mockResolvedValue({ data: null }),
    setExerciseEntries: vi.fn(),
    setTemporarySubstitutions: vi.fn(),
    substituteExercise: { mutateAsync: vi.fn().mockResolvedValue(undefined) },
    updateExercisesMutation: { mutateAsync: vi.fn().mockResolvedValue(undefined) },
  };
}

describe('session exercise changes and Hevy', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('saves a weight change and points at the Settings push when a key is stored', async () => {
    vi.mocked(hevyApi.isConfigured).mockReturnValue(true);
    const deps = makeDeps();

    await createSaveExerciseConfigHandler(deps)('ex-1', { weightValue: 15, weightUnit: 1 });

    expect(deps.updateExercisesMutation.mutateAsync).toHaveBeenCalledTimes(1);
    expect(toast.success).toHaveBeenCalledWith(`Exercise configuration saved! ${PUSH_AGAIN_HINT}`, { id: 'save-exercise-config' });
    expect(hevyApi.getAllRoutines).not.toHaveBeenCalled();
    expect(hevyApi.deleteRoutine).not.toHaveBeenCalled();
  });

  it('saves a weight change without mentioning Hevy when no key is stored', async () => {
    vi.mocked(hevyApi.isConfigured).mockReturnValue(false);
    const deps = makeDeps();

    await createSaveExerciseConfigHandler(deps)('ex-1', { weightValue: 15, weightUnit: 1 });

    expect(toast.success).toHaveBeenCalledWith('Exercise configuration saved!', { id: 'save-exercise-config' });
  });

  it('changes the progression and points at the Settings push when a key is stored', async () => {
    vi.mocked(hevyApi.isConfigured).mockReturnValue(true);
    const deps = makeDeps();

    await createChangeProgressionHandler(deps)('ex-1', { type: 'MinimalSets' } as never);

    expect(deps.substituteExercise.mutateAsync).toHaveBeenCalledTimes(1);
    expect(toast.success).toHaveBeenCalledWith(
      `Changed Lateral Raise (Cable) to MinimalSets progression. ${PUSH_AGAIN_HINT}`,
      { id: 'change-progression' },
    );
    expect(hevyApi.getAllRoutines).not.toHaveBeenCalled();
    expect(hevyApi.deleteRoutine).not.toHaveBeenCalled();
  });
});

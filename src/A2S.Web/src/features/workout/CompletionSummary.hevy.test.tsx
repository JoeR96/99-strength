/** Pins audit §2.3: the Hevy title uses the week that was completed, not the refetched one. */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { CompletionSummary } from './CompletionSummary';
import { createCompletedWorkoutInHevy } from '@/services/hevySyncService';
import type { CompleteDayResult, WorkoutDto, ExerciseEntry } from './workoutSessionTypes';

vi.mock('@/components/layout/Navbar', () => ({ Navbar: () => null }));
vi.mock('@/contexts/HevyContext', () => ({ useHevy: () => ({ isConfigured: true, isValid: true }) }));
vi.mock('@/services/hevySyncService', () => ({
  createCompletedWorkoutInHevy: vi.fn().mockResolvedValue({ success: true, message: 'synced' }),
  handleRoutineLifecycle: vi.fn().mockResolvedValue({ success: true, message: 'ok' }),
}));
vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

// After the refetch the workout is already on week 12…
const workout = { id: 'w-1', name: 'Cut', currentWeek: 12, totalWeeks: 21, daysPerWeek: 4, exercises: [] } as unknown as WorkoutDto;

// …but the day that was just completed belonged to week 11.
const result = {
  workoutId: 'w-1',
  day: 4,
  weekNumber: 11,
  blockNumber: 2,
  exercisesCompleted: 1,
  progressionChanges: [],
  newCurrentWeek: 12,
  newCurrentDay: 1,
  weekProgressed: true,
  programComplete: false,
  isDeloadWeek: false,
  exercisesPendingWeightConfirmation: [],
  nextSessionExercises: [],
} as unknown as CompleteDayResult;

const entries = [
  {
    exercise: { id: 'ex-1', name: 'Bench' },
    sets: [{ setNumber: 1, weight: 80, reps: 8, isAmrap: false, completed: true }],
    targetSets: 1,
    targetReps: 8,
    targetWeight: 80,
    weightUnit: 'kg',
    isAmrapExercise: false,
  },
] as unknown as ExerciseEntry[];

describe('CompletionSummary → Hevy', () => {
  it('sends the completed week number so the title is not stamped with the new week', async () => {
    render(
      <CompletionSummary
        result={result}
        workout={workout}
        dayNumber={4}
        dayName="Thursday"
        exerciseEntries={entries}
        workoutStartTime={new Date('2026-11-12T17:00:00Z')}
        workoutEndTime={new Date('2026-11-12T17:50:00Z')}
        onContinue={vi.fn()}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /send to hevy/i }));
    await waitFor(() => expect(createCompletedWorkoutInHevy).toHaveBeenCalledTimes(1));
    const args = vi.mocked(createCompletedWorkoutInHevy).mock.calls[0];
    expect(args[6]).toBe(11);
    expect(args[0].currentWeek).toBe(12);
  });
});

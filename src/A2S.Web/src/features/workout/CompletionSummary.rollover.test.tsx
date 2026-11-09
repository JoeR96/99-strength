/**
 * Pins defect 9 of the live walkthrough: when a week rolls over, the summary no longer builds
 * next week's Hevy routine (and deletes last week's) from the browser. Nothing goes to Hevy
 * until Send to Hevy is tapped.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CompletionSummary } from './CompletionSummary';
import { hevyApi } from '@/services/hevyApi';
import type { CompleteDayResult, WorkoutDto, ExerciseEntry } from './workoutSessionTypes';

vi.mock('@/components/layout/Navbar', () => ({ Navbar: () => null }));
vi.mock('@/contexts/HevyContext', () => ({ useHevy: () => ({ isConfigured: true, isValid: true }) }));
vi.mock('@/api/apiClient', () => {
  const apiClient = { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() };
  return { apiClient, default: apiClient };
});
vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

const workout = { id: 'w-1', name: 'Cut', currentWeek: 12, totalWeeks: 21, daysPerWeek: 4, exercises: [] } as unknown as WorkoutDto;

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

describe('CompletionSummary at week rollover', () => {
  it('makes no Hevy call by itself', async () => {
    hevyApi.setConfigured(true);
    const { apiClient } = await import('@/api/apiClient');

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

    expect(await screen.findByTestId('week-progressed-notice')).toHaveTextContent('Moving to Week 12');
    expect(screen.getByRole('button', { name: 'Send to Hevy' })).toBeEnabled();
    // Let any effect that would have fired on mount run before looking.
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(apiClient.get).not.toHaveBeenCalled();
    expect(apiClient.post).not.toHaveBeenCalled();
    expect(apiClient.put).not.toHaveBeenCalled();
    expect(apiClient.delete).not.toHaveBeenCalled();
  });
});

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CompletionSummary } from './CompletionSummary';
import type { CompleteDayResult, WorkoutDto, ExerciseEntry } from './workoutSessionTypes';

vi.mock('@/components/layout/Navbar', () => ({
  Navbar: () => null,
}));

vi.mock('@/contexts/HevyContext', () => ({
  useHevy: () => ({ isConfigured: false, isValid: false }),
}));

vi.mock('@/services/hevySyncService', () => ({
  createCompletedWorkoutInHevy: vi.fn(),
  handleRoutineLifecycle: vi.fn(),
}));

const workout = {
  id: 'w-1',
  totalWeeks: 21,
  daysPerWeek: 4,
  exercises: [],
} as unknown as WorkoutDto;

// The session the user just finished: lifted 20 kg for 5x12
const exerciseEntries = [
  {
    exercise: { id: 'ex-1', name: 'Lateral Raise (Cable)' },
    sets: [],
    targetSets: 5,
    targetReps: 12,
    targetWeight: 20,
    weightUnit: 'kg',
    isAmrapExercise: false,
  },
] as unknown as ExerciseEntry[];

function makeResult(overrides: Partial<CompleteDayResult> = {}): CompleteDayResult {
  return {
    workoutId: 'w-1',
    day: 1,
    weekNumber: 1,
    blockNumber: 1,
    exercisesCompleted: 1,
    progressionChanges: [
      { exerciseId: 'ex-1', exerciseName: 'Lateral Raise (Cable)', change: 'Weight increased to 22.5 kg' },
    ],
    newCurrentWeek: 1,
    newCurrentDay: 2,
    weekProgressed: false,
    programComplete: false,
    isDeloadWeek: false,
    exercisesPendingWeightConfirmation: [],
    nextSessionExercises: [
      {
        exerciseId: 'ex-1',
        exerciseName: 'Lateral Raise (Cable)',
        setCount: 5,
        targetReps: 12,
        weight: 22.5,
        weightUnit: 'Kilograms',
        hasAmrap: false,
      },
    ],
    ...overrides,
  } as CompleteDayResult;
}

function renderSummary(result: CompleteDayResult) {
  return render(
    <CompletionSummary
      result={result}
      workout={workout}
      dayNumber={1}
      dayName="Day 1"
      exerciseEntries={exerciseEntries}
      workoutStartTime={new Date('2026-07-16T10:00:00Z')}
      workoutEndTime={new Date('2026-07-16T11:00:00Z')}
      onContinue={vi.fn()}
    />
  );
}

describe('CompletionSummary next-session preview', () => {
  it('shows the backend-computed next-session plan, not the just-completed weights', () => {
    renderSummary(makeResult());

    expect(screen.getByTestId('next-session-title')).toHaveTextContent('Next Day 1 Session (Week 2)');
    expect(screen.getByTestId('next-weight-0')).toHaveTextContent('22.5 kg');
    expect(screen.getByTestId('next-weight-0')).not.toHaveTextContent('20');
    expect(screen.getByTestId('next-sets-0')).toHaveTextContent('5 sets');
    expect(screen.getByTestId('next-reps-0')).toHaveTextContent('12 reps');
  });

  it('hides the next-session card when there is no next session and the program continues', () => {
    renderSummary(makeResult({ nextSessionExercises: [] }));

    expect(screen.queryByTestId('next-session-title')).not.toBeInTheDocument();
  });

  it('shows the final session summary when the program is complete', () => {
    renderSummary(makeResult({ programComplete: true, nextSessionExercises: [] }));

    expect(screen.getByTestId('next-session-title')).toHaveTextContent('Final Session Summary');
    expect(screen.getByTestId('completion-title')).toHaveTextContent('Program Complete!');
  });
});

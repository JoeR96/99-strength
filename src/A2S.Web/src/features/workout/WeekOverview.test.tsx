/**
 * Pins defect 9 of the live walkthrough: the Plan tab no longer builds Hevy routines in the
 * browser. Where the Send buttons were it points at the server-side push on Today and Settings.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { WeekOverview } from './WeekOverview';
import { NextWeekPreview } from './NextWeekPreview';
import { WeightUnit, type ExerciseDto, type WorkoutDto } from '@/types/workout';

const hevy = vi.hoisted(() => ({ isConfigured: true, isValid: true as boolean | null }));

vi.mock('@/contexts/HevyContext', () => ({ useHevy: () => hevy }));
vi.mock('@/hooks/useWorkouts', () => ({
  useSubstituteExercise: () => ({ mutateAsync: vi.fn() }),
  useUpdateExercises: () => ({ mutateAsync: vi.fn() }),
  useUndoCompletion: () => ({ mutateAsync: vi.fn() }),
  useRemoveExercise: () => ({ mutateAsync: vi.fn() }),
}));
vi.mock('@/services/hevySyncService', () => ({ pullWorkoutFromHevy: vi.fn() }));
vi.mock('@/services/hevyApi', () => ({ hevyApi: { isConfigured: () => true, getAllExerciseTemplates: vi.fn() } }));
vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn(), loading: vi.fn() }) }));

function exercise(day: number): ExerciseDto {
  return {
    id: `ex-${day}`,
    name: `Squat ${day}`,
    category: 1,
    equipment: 0,
    assignedDay: day,
    orderInDay: 1,
    hevyExerciseTemplateId: 'ABCD1234',
    progression: { type: 'Linear', trainingMax: { value: 100, unit: WeightUnit.Kilograms }, useAmrap: true, baseSetsPerExercise: 5 },
  } as unknown as ExerciseDto;
}

const workout = {
  id: 'w-1',
  name: 'Cut',
  currentWeek: 2,
  currentDay: 2,
  totalWeeks: 21,
  daysPerWeek: 4,
  blockSequence: [1, 2, 3],
  // Day 1 is done, so day 1 of next week is revealed in the preview; day 2 is the current day.
  completedDaysInCurrentWeek: [1],
  isWeekComplete: false,
  exercises: [1, 2, 3, 4].map(exercise),
} as unknown as WorkoutDto;

function renderOverview() {
  return render(
    <MemoryRouter>
      <WeekOverview workout={workout} />
    </MemoryRouter>,
  );
}

describe('WeekOverview and Hevy', () => {
  beforeEach(() => {
    hevy.isConfigured = true;
    hevy.isValid = true;
  });

  it('has no button that builds a routine in the browser', () => {
    renderOverview();

    expect(screen.queryByRole('button', { name: /send week to hevy/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /send to hevy/i })).not.toBeInTheDocument();
    expect(screen.queryByTestId('send-to-hevy-day-2')).not.toBeInTheDocument();
  });

  it('points at the server-side push on Today and in Settings when a key is stored', () => {
    renderOverview();

    const pointer = within(screen.getByTestId('hevy-push-pointer'));
    expect(pointer.getByRole('link', { name: 'Push to Hevy on Today' })).toHaveAttribute('href', '/today');
    expect(pointer.getByRole('link', { name: 'Push this week in Settings' })).toHaveAttribute('href', '/settings');
  });

  it('says nothing about Hevy without a key', () => {
    hevy.isConfigured = false;
    hevy.isValid = null;
    renderOverview();

    expect(screen.queryByTestId('hevy-push-pointer')).not.toBeInTheDocument();
    expect(screen.queryByTestId('pull-workout-day-2')).not.toBeInTheDocument();
  });

  it('keeps Pull from Hevy on the current day', () => {
    renderOverview();

    expect(screen.getByTestId('pull-workout-day-2')).toHaveTextContent('Pull from Hevy');
  });
});

describe('NextWeekPreview and Hevy', () => {
  it('shows next week with no Hevy buttons, revealed day included', () => {
    render(<NextWeekPreview workout={workout} />);

    expect(screen.getByText('Next Week Preview')).toBeInTheDocument();
    expect(screen.getByText('W3 D1')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByText(/hevy/i)).not.toBeInTheDocument();
  });
});

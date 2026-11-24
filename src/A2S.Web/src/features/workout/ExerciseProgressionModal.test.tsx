import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { ExerciseProgressionModal } from './ExerciseProgressionModal';
import type { ExerciseDto, WorkoutHistoryResponse } from '@/types/workout';

const history = {
  exerciseHistories: [
    {
      exerciseId: 'stairs',
      name: 'Stair Machine (Floors)',
      progressionType: 'CardioStage',
      settingsChanges: [
        { occurredAt: '2026-11-24T09:00:00Z', weekNumber: 1, dayNumber: 1, oldText: '10 min @ ? % incline', newText: '10 min @ 5 % incline, 5.6 km/h', reason: 'Cardio settings changed' },
      ],
      weeklyHistory: [
        { weekNumber: 1, blockNumber: 1, isDeloadWeek: false, cardioSummary: '5 min @ level 6 · Easy', sets: [] },
        { weekNumber: 2, blockNumber: 1, isDeloadWeek: false, cardioSummary: '6 min @ level 6 · Solid', sets: [] },
      ],
    },
  ],
} as unknown as WorkoutHistoryResponse;

vi.mock('@/hooks/useWorkouts', () => ({
  useWorkoutHistory: () => ({ data: history, isLoading: false }),
}));

const exercise = {
  id: 'stairs',
  name: 'Stair Machine (Floors)',
  assignedDay: 1,
  progression: { type: 'CardioStage', lane: 'Hard', stageNumber: 1, stageName: 'Continuous', prescription: '6 min @ level 6', isSettingsPending: false },
} as unknown as ExerciseDto;

describe('ExerciseProgressionModal cardio', () => {
  it('progression modal for a cardio exercise lists sessions and settings changes, and offers no progression-type change', () => {
    render(
      <ExerciseProgressionModal
        exercise={exercise}
        workoutId="w1"
        blockSequence={[1, 2, 3]}
        isOpen
        onClose={() => {}}
        onChangeProgression={async () => {}}
        onSave={async () => {}}
      />,
    );

    const changes = screen.getByTestId('cardio-settings-changes');
    expect(changes).toHaveTextContent('settings: 10 min @ ? % incline → 10 min @ 5 % incline, 5.6 km/h');
    const rows = within(screen.getByTestId('cardio-sessions')).getAllByRole('listitem').map((li) => li.textContent);
    expect(rows[0]).toContain('6 min @ level 6 · Solid');
    expect(rows[1]).toContain('5 min @ level 6 · Easy');
    expect(screen.getByText('Cardio stage')).toBeInTheDocument();
    expect(screen.getByText(/1 · Continuous/)).toBeInTheDocument();
    expect(screen.queryByText('Change Progression Type')).not.toBeInTheDocument();
  });
});

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ExerciseProgressionModal } from './ExerciseProgressionModal';
import type { ExerciseDto, WorkoutHistoryResponse } from '@/types/workout';

const history = {
  exerciseHistories: [
    {
      exerciseId: 'stairs',
      name: 'Stair Machine (Floors)',
      progressionType: 'CardioStage',
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
  it('lists the cardio sessions newest first with the stage, and offers no progression-type change', () => {
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

    const rows = screen.getAllByRole('listitem').map((li) => li.textContent);
    expect(rows[0]).toContain('6 min @ level 6 · Solid');
    expect(rows[1]).toContain('5 min @ level 6 · Easy');
    expect(screen.getByText('Cardio stage')).toBeInTheDocument();
    expect(screen.getByText(/1 · Continuous/)).toBeInTheDocument();
    expect(screen.queryByText('Change Progression Type')).not.toBeInTheDocument();
  });
});

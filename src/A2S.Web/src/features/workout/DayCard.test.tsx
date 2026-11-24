import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DayCard } from './DayCard';
import type { CardioStageProgressionDto, ExerciseDto } from '@/types/workout';

function stairs(overrides: Partial<CardioStageProgressionDto> = {}): ExerciseDto {
  const progression: CardioStageProgressionDto = {
    type: 'CardioStage',
    lane: 'Hard',
    stageNumber: 1,
    stageName: 'Continuous',
    prescription: '3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)',
    isSettingsPending: false,
    ...overrides,
  };
  return { id: 'stairs', name: 'Stair Machine (Floors)', assignedDay: 1, orderInDay: 7, progression } as unknown as ExerciseDto;
}

const baseProps = {
  weekNumber: 1,
  dayNumber: 1,
  isCompleted: false,
  isCurrent: false,
  hevyEnabled: false,
  onEdit: () => {},
  onPullWorkout: () => {},
  onSubstituteExercise: () => {},
  blockSequence: [1, 2, 3],
};

describe('DayCard cardio exercise', () => {
  it('Full plan shows the stair climber on Upper A with its stage and prescription, not sets', () => {
    render(<DayCard {...baseProps} exercises={[stairs()]} />);

    expect(screen.getByText('Stair Machine (Floors)')).toBeInTheDocument();
    expect(screen.getByTestId('cardio-stage')).toHaveTextContent('1 · Continuous');
    expect(screen.getByTestId('cardio-stage')).toHaveTextContent('6 min @ level 6');
    expect(screen.queryByText('Sets × Reps:')).not.toBeInTheDocument();
  });

  it('asks for the settings first while they are not set', () => {
    render(<DayCard {...baseProps} exercises={[stairs({ isSettingsPending: true })]} />);

    expect(screen.getByTestId('cardio-stage')).toHaveTextContent('Set in gym mode first');
  });
});

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { WorkoutActivityDetail, type ExerciseHistoryDto, type WorkoutActivityDto } from './WorkoutHistoryComponents';

const activity: WorkoutActivityDto = {
  day: 'Day1',
  dayNumber: 1,
  weekNumber: 1,
  blockNumber: 1,
  completedAt: '2026-11-23T18:00:00Z',
  isDeloadWeek: false,
  performances: [
    { exerciseId: 'squat', completedAt: '2026-11-23T18:00:00Z', completedSets: [{ setNumber: 1, weight: 100, weightUnit: 'Kilograms', actualReps: 5, wasAmrap: false }] },
    { exerciseId: 'stairs', completedAt: '2026-11-23T18:30:00Z', completedSets: [], cardioSummary: '7 min @ level 6 · Easy' },
  ],
};

const histories = [
  { exerciseId: 'squat', name: 'Squat Barbell' },
  { exerciseId: 'stairs', name: 'Stair Machine (Floors)' },
] as ExerciseHistoryDto[];

describe('WorkoutActivityDetail', () => {
  it('lists a cardio performance as "7 min @ level 6 · Easy" and adds no sets or volume for it', () => {
    render(<WorkoutActivityDetail activity={activity} date={new Date('2026-11-23T18:00:00Z')} exerciseHistories={histories} onClose={() => {}} />);

    expect(screen.getByTestId('cardio-summary')).toHaveTextContent('7 min @ level 6 · Easy');
    expect(screen.getByText('Stair Machine (Floors)')).toBeInTheDocument();
    expect(screen.getByText('500')).toBeInTheDocument();
  });
});

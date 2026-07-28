import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ExerciseCard } from './ExerciseCard';
import type { ExerciseEntry } from './workoutSessionTypes';

function makeEntry(overrides: Partial<ExerciseEntry> = {}): ExerciseEntry {
  return {
    exercise: {
      id: 'ex-1',
      name: 'Squat Barbell',
      progression: { type: 'Linear' },
    } as ExerciseEntry['exercise'],
    sets: [
      { setNumber: 1, weight: 100, reps: 10, isAmrap: false, completed: true },
      { setNumber: 2, weight: 100, reps: 10, isAmrap: false, completed: true },
    ],
    targetSets: 2,
    targetReps: 10,
    targetWeight: 100,
    weightUnit: 'kg',
    isAmrapExercise: false,
    ...overrides,
  };
}

const noop = () => {};

describe('ExerciseCard collapse/expand', () => {
  it('expands a defaultCollapsed completed card on click, then re-collapses via the collapse button', () => {
    const entry = makeEntry();
    render(
      <ExerciseCard
        entry={entry}
        exerciseIndex={0}
        onSetChange={noop}
        onSetComplete={noop}
        onSubstitute={noop}
        onEdit={noop}
        isTemporarilySubstituted={false}
        defaultCollapsed={true}
      />
    );

    // Starts collapsed: shows summary header with aria-expanded=false.
    const collapsedHeader = screen.getByRole('button', { name: /expand squat barbell/i });
    expect(collapsedHeader).toHaveAttribute('aria-expanded', 'false');

    // Expand it.
    fireEvent.click(collapsedHeader);

    // Now the full weight/reps grid is visible (Weight (kg) column header).
    expect(screen.getByText('Weight (kg)')).toBeInTheDocument();

    // A collapse affordance must exist and be able to re-collapse the card.
    const collapseButton = screen.getByTestId('collapse-exercise-squat-barbell');
    expect(collapseButton).toHaveAttribute('aria-expanded', 'true');

    fireEvent.click(collapseButton);

    // Back to the collapsed summary view.
    expect(screen.queryByText('Weight (kg)')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /expand squat barbell/i })).toHaveAttribute(
      'aria-expanded',
      'false'
    );
  });

  it('does not show a collapse button on an expanded but incomplete card', () => {
    const entry = makeEntry({
      sets: [{ setNumber: 1, weight: 100, reps: 10, isAmrap: false, completed: false }],
    });
    render(
      <ExerciseCard
        entry={entry}
        exerciseIndex={0}
        onSetChange={noop}
        onSetComplete={noop}
        onSubstitute={noop}
        onEdit={noop}
        isTemporarilySubstituted={false}
      />
    );

    expect(screen.queryByTestId('collapse-exercise-squat-barbell')).not.toBeInTheDocument();
  });
});

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { WeightConfirmationModal } from './WeightConfirmationModal';
import type { PendingWeightExerciseDto } from '@/types/workout';

const mockExercises: PendingWeightExerciseDto[] = [
  { exerciseId: 'ex-1', exerciseName: 'Lat Pulldown', suggestedWeight: 40, weightUnit: 'Kilograms', confirmationType: 'StartingWeight' },
  { exerciseId: 'ex-2', exerciseName: 'Bicep Curl', suggestedWeight: 15, weightUnit: 'Pounds', confirmationType: 'StartingWeight' },
];

const mockWorkingWeightExercises: PendingWeightExerciseDto[] = [
  { exerciseId: 'ex-3', exerciseName: 'Triceps Pushdown', suggestedWeight: 32.5, weightUnit: 'Kilograms', confirmationType: 'WorkingWeight' },
];

describe('WeightConfirmationModal', () => {
  it('renders exercise names and suggested weights', () => {
    render(
      <WeightConfirmationModal
        exercises={mockExercises}
        phase="pre-completion"
        onConfirm={vi.fn()}
        onSkip={vi.fn()}
      />
    );

    expect(screen.getByText('Confirm Starting Weights')).toBeInTheDocument();
    expect(screen.getByText('Lat Pulldown')).toBeInTheDocument();
    expect(screen.getByText('Bicep Curl')).toBeInTheDocument();
    expect(screen.getByText(/Suggested: 40 kg/)).toBeInTheDocument();
    expect(screen.getByText(/Suggested: 15 lbs/)).toBeInTheDocument();
  });

  it('shows working-weight copy in post-completion phase', () => {
    render(
      <WeightConfirmationModal
        exercises={mockWorkingWeightExercises}
        phase="post-completion"
        onConfirm={vi.fn()}
        onSkip={vi.fn()}
      />
    );

    expect(screen.getByText('Confirm New Working Weights')).toBeInTheDocument();
    expect(screen.getByText('Skip for now')).toBeInTheDocument();
    expect(screen.getByText('Confirm Weights')).toBeInTheDocument();
  });

  it('shows cancel + confirm-and-complete buttons in pre-completion phase', () => {
    render(
      <WeightConfirmationModal
        exercises={mockExercises}
        phase="pre-completion"
        onConfirm={vi.fn()}
        onSkip={vi.fn()}
      />
    );

    expect(screen.getByText('Cancel')).toBeInTheDocument();
    expect(screen.getByText('Confirm & Complete Workout')).toBeInTheDocument();
    expect(screen.queryByText('Skip for now')).not.toBeInTheDocument();
  });

  it('pre-fills weight inputs with suggested values', () => {
    render(
      <WeightConfirmationModal
        exercises={mockExercises}
        phase="pre-completion"
        onConfirm={vi.fn()}
        onSkip={vi.fn()}
      />
    );

    const inputs = screen.getAllByRole('spinbutton');
    expect(inputs).toHaveLength(2);
    expect(inputs[0]).toHaveValue(40);
    expect(inputs[1]).toHaveValue(15);
  });

  it('calls onConfirm with correct payload including adjusted weights', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(
      <WeightConfirmationModal
        exercises={mockExercises}
        phase="pre-completion"
        onConfirm={onConfirm}
        onSkip={vi.fn()}
      />
    );

    // Adjust first exercise weight
    const inputs = screen.getAllByRole('spinbutton');
    fireEvent.change(inputs[0], { target: { value: '45' } });

    fireEvent.click(screen.getByText('Confirm & Complete Workout'));

    await waitFor(() => {
      expect(onConfirm).toHaveBeenCalledWith([
        { exerciseId: 'ex-1', weight: 45, unit: 1 },
        { exerciseId: 'ex-2', weight: 15, unit: 2 },
      ]);
    });
  });

  it('calls onSkip when cancel button is clicked in pre-completion phase', () => {
    const onSkip = vi.fn();
    render(
      <WeightConfirmationModal
        exercises={mockExercises}
        phase="pre-completion"
        onConfirm={vi.fn()}
        onSkip={onSkip}
      />
    );

    fireEvent.click(screen.getByText('Cancel'));
    expect(onSkip).toHaveBeenCalled();
  });

  it('calls onSkip when skip button is clicked in post-completion phase', () => {
    const onSkip = vi.fn();
    render(
      <WeightConfirmationModal
        exercises={mockWorkingWeightExercises}
        phase="post-completion"
        onConfirm={vi.fn()}
        onSkip={onSkip}
      />
    );

    fireEvent.click(screen.getByText('Skip for now'));
    expect(onSkip).toHaveBeenCalled();
  });

  it('shows confirming state while confirming', async () => {
    let resolveConfirm: () => void;
    const onConfirm = vi.fn().mockReturnValue(new Promise<void>((resolve) => { resolveConfirm = resolve; }));

    render(
      <WeightConfirmationModal
        exercises={mockWorkingWeightExercises}
        phase="post-completion"
        onConfirm={onConfirm}
        onSkip={vi.fn()}
      />
    );

    fireEvent.click(screen.getByText('Confirm Weights'));

    await waitFor(() => {
      expect(screen.getByText('Confirming...')).toBeInTheDocument();
    });

    resolveConfirm!();
  });
});

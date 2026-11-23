import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AddToPlanPanel } from './AddToPlanPanel';

const mutate = vi.fn();
vi.mock('@/hooks/useWorkouts', () => ({
  useCurrentWorkout: () => ({
    data: {
      id: 'w1',
      name: 'Plan v3',
      exercises: [
        { name: 'Bench Press (Barbell)', assignedDay: 1, orderInDay: 1 },
        { name: 'Skullcrusher (Dumbbell)', assignedDay: 1, orderInDay: 5 },
        { name: 'Cable Core Palloff Press', assignedDay: 1, orderInDay: 6 },
        { name: 'Front Squat', assignedDay: 4, orderInDay: 1 },
      ],
    },
  }),
  useAddExercise: () => ({ mutate, isPending: false, isSuccess: false, isError: false }),
}));

describe('AddToPlanPanel', () => {
  beforeEach(() => mutate.mockReset());

  it('adds a reps-per-set accessory before the chosen exercise with a starting weight', () => {
    render(<AddToPlanPanel templateName="Bicep Curl (Dumbbell)" hevyTemplateId="37FCC2BB" />);

    fireEvent.change(screen.getByLabelText('Position'), { target: { value: '6' } });
    fireEvent.change(screen.getByLabelText('Weight (kg)'), { target: { value: '14' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add to plan' }));

    expect(mutate).toHaveBeenCalledWith({
      workoutId: 'w1',
      request: expect.objectContaining({
        templateName: 'Bicep Curl (Dumbbell)',
        externalTemplateId: '37FCC2BB',
        progressionType: 'RepsPerSet',
        assignedDay: 1,
        orderInDay: 6,
        startingSets: 2,
        repRangeMinimum: 10,
        repRangeMaximum: 12,
        startingWeight: 14,
        weightUnit: 1,
      }),
    });
  });

  it('leaves the weight to confirm later when it is blank, and blocks an inverted rep range', () => {
    render(<AddToPlanPanel templateName="Bicep Curl (Dumbbell)" hevyTemplateId="37FCC2BB" />);

    fireEvent.change(screen.getByLabelText('Minimum reps'), { target: { value: '15' } });
    expect(screen.getByRole('button', { name: 'Add to plan' })).toBeDisabled();

    fireEvent.change(screen.getByLabelText('Minimum reps'), { target: { value: '10' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add to plan' }));
    expect(mutate.mock.calls[0][0].request).not.toHaveProperty('startingWeight');
    expect(mutate.mock.calls[0][0].request.orderInDay).toBe(7);
  });
});

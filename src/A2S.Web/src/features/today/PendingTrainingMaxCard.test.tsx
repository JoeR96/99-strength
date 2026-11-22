import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { workoutsApi } from '@/api/workouts';
import { activeWorkoutFixture } from '@/mocks/handlers/workouts';
import type { ExerciseDto } from '@/types/workout';
import { PendingTrainingMaxCard } from './PendingTrainingMaxCard';

vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

const bench = { id: 'v3-bench', name: 'Bench Press (Barbell)', isTrainingMaxPending: true, progression: { type: 'Linear' } } as unknown as ExerciseDto;
const row = { id: 'v3-row', name: 'Bent Over Row (Barbell)', isTrainingMaxPending: false, progression: { type: 'RepsPerSet' } } as unknown as ExerciseDto;

function renderCard() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <PendingTrainingMaxCard />
    </QueryClientProvider>
  );
}

describe('PendingTrainingMaxCard', () => {
  it('lists lifts without a training max and sets one from a recent set', async () => {
    vi.spyOn(workoutsApi, 'getCurrentWorkout').mockResolvedValue({ ...activeWorkoutFixture, exercises: [bench, row] });
    const save = vi.spyOn(workoutsApi, 'setTrainingMax').mockResolvedValue({ exerciseId: 'v3-bench', trainingMax: 80, unit: 'Kilograms', estimated: true });
    renderCard();

    fireEvent.click(await screen.findByRole('button', { name: 'Set training max for Bench Press (Barbell)' }));
    expect(screen.queryByText('Bent Over Row (Barbell)')).toBeNull();
    fireEvent.change(screen.getByLabelText('Weight (kg)'), { target: { value: '80' } });
    fireEvent.change(screen.getByLabelText('Reps'), { target: { value: '5' } });
    fireEvent.click(screen.getByTestId('tm-save'));

    await waitFor(() => expect(save).toHaveBeenCalledWith('w-1', 'v3-bench', { recentWeight: 80, recentReps: 5, unit: 'Kilograms' }));
  });

  it('can enter the training max directly', async () => {
    vi.spyOn(workoutsApi, 'getCurrentWorkout').mockResolvedValue({ ...activeWorkoutFixture, exercises: [bench] });
    const save = vi.spyOn(workoutsApi, 'setTrainingMax').mockResolvedValue({ exerciseId: 'v3-bench', trainingMax: 75, unit: 'Kilograms', estimated: false });
    renderCard();

    fireEvent.click(await screen.findByRole('button', { name: 'Set training max for Bench Press (Barbell)' }));
    fireEvent.click(screen.getByRole('button', { name: 'Enter TM' }));
    fireEvent.change(screen.getByLabelText('Training max (kg)'), { target: { value: '75' } });
    fireEvent.click(screen.getByTestId('tm-save'));

    await waitFor(() => expect(save).toHaveBeenCalledWith('w-1', 'v3-bench', { trainingMax: 75, unit: 'Kilograms' }));
  });

  it('shows nothing when every lift has a training max', async () => {
    const get = vi.spyOn(workoutsApi, 'getCurrentWorkout').mockResolvedValue({ ...activeWorkoutFixture, exercises: [row] });
    const { container } = renderCard();

    await waitFor(() => expect(get).toHaveBeenCalled());
    expect(container.textContent).toBe('');
  });
});

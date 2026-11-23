import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { workoutsApi } from '@/api/workouts';
import { installHandlers } from '@/mocks/handlers/install';
import { BENCH_ID, startingNumbers } from '@/mocks/fixtures/startingNumbers';
import { SetTrainingMaxDialog } from './SetTrainingMaxDialog';

const setTrainingMax = vi.fn(async (_w: string, exerciseId: string) => ({ exerciseId, trainingMax: 1, unit: 'Kilograms', estimated: false }));

function renderDialog() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <SetTrainingMaxDialog open workoutId="w-1" exerciseId={BENCH_ID} exerciseName="Bench Press (Barbell)" onClose={() => {}} />
    </QueryClientProvider>
  );
}

describe('SetTrainingMaxDialog suggestions', () => {
  beforeEach(() => {
    setTrainingMax.mockClear();
    installHandlers(workoutsApi, { setTrainingMax });
  });

  it('prefills the Hevy suggestion with its evidence and saves it', async () => {
    installHandlers(workoutsApi, { getStartingNumberSuggestions: async () => startingNumbers.fresh() });
    renderDialog();

    expect(await screen.findByTestId('suggestion-evidence')).toHaveTextContent('Suggested 77.5 kg — from 75 kg × 5 on 14 Nov (est. 1RM 87.5 kg)');
    expect(screen.getByLabelText('Training max (kg)')).toHaveValue('77.5');
    fireEvent.click(screen.getByTestId('tm-save'));

    await waitFor(() => expect(setTrainingMax).toHaveBeenCalledWith('w-1', BENCH_ID, { trainingMax: 77.5, unit: 'Kilograms' }));
  });

  it('a typed value overrides the suggestion', async () => {
    installHandlers(workoutsApi, { getStartingNumberSuggestions: async () => startingNumbers.fresh() });
    renderDialog();

    fireEvent.change(await screen.findByLabelText('Training max (kg)'), { target: { value: '70' } });
    fireEvent.click(screen.getByTestId('tm-save'));

    await waitFor(() => expect(setTrainingMax).toHaveBeenCalledWith('w-1', BENCH_ID, { trainingMax: 70, unit: 'Kilograms' }));
  });

  it('Use suggestion puts the suggestion back after switching to a recent set', async () => {
    installHandlers(workoutsApi, { getStartingNumberSuggestions: async () => startingNumbers.stale() });
    renderDialog();

    expect(await screen.findByTestId('suggestion-evidence')).toHaveTextContent('older history — estimated');
    fireEvent.click(screen.getByRole('button', { name: 'From a recent set' }));
    fireEvent.click(screen.getByTestId('use-suggestion'));

    expect(screen.getByLabelText('Training max (kg)')).toHaveValue('80');
  });

  it('falls back to the plain input when Hevy is unavailable', async () => {
    installHandlers(workoutsApi, { getStartingNumberSuggestions: async () => startingNumbers.unavailable() });
    renderDialog();

    expect(await screen.findByTestId('suggestion-fallback')).toHaveTextContent('Hevy unavailable — enter it yourself.');
    expect(screen.queryByTestId('suggestion-evidence')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Weight (kg)')).toHaveValue('');
  });
});

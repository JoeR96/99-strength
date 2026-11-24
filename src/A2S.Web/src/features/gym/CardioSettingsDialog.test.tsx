import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { workoutsApi, type PlannedCardioBlockDto } from '@/api/workouts';
import type { ExerciseDto } from '@/types/workout';
import { CardioSettingsDialog } from './CardioSettingsDialog';
import { stairStage2PendingBlock, walkPendingBlock } from './gymFixtures';

function renderDialog(block: PlannedCardioBlockDto, exerciseId: string, onClose = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <CardioSettingsDialog open workoutId="w-v3" exerciseId={exerciseId} name="Cardio" block={block} onClose={onClose} />
    </QueryClientProvider>
  );
  return onClose;
}

describe('CardioSettingsDialog', () => {
  const save = vi.fn(async () => ({ id: 'x' }) as ExerciseDto);

  beforeEach(() => {
    save.mockClear();
    vi.spyOn(workoutsApi, 'setCardioSettings').mockImplementation(save);
  });

  it('asks for the hard level at stage 2 with the suggestion prefilled', async () => {
    const onClose = renderDialog(stairStage2PendingBlock, 'v3-stairs');

    expect(screen.getByRole('dialog')).toHaveTextContent('a level you could hold for 2 hard minutes, 4 times');
    expect(screen.getByLabelText('Hard level')).toHaveValue(8);
    expect(screen.queryByLabelText('Speed (km/h)')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('cardio-settings-save'));

    await waitFor(() => expect(save).toHaveBeenCalledWith('w-v3', 'v3-stairs', { intensity: 8 }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('asks the walk for speed and incline, 5 % prefilled, and refuses a speed out of range', async () => {
    renderDialog(walkPendingBlock, 'v3-walk');

    expect(screen.getByRole('dialog')).toHaveTextContent('a brisk walk where you can still talk in full sentences');
    expect(screen.getByLabelText('Incline (%)')).toHaveValue(5);
    fireEvent.change(screen.getByLabelText('Speed (km/h)'), { target: { value: '30' } });
    fireEvent.click(screen.getByTestId('cardio-settings-save'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Speed between 0.5 and 25 km/h');
    expect(save).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText('Speed (km/h)'), { target: { value: '5.5' } });
    fireEvent.click(screen.getByTestId('cardio-settings-save'));
    await waitFor(() => expect(save).toHaveBeenCalledWith('w-v3', 'v3-walk', { intensity: 5, speed: 5.5 }));
  });
});

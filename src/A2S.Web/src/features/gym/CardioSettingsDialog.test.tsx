import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { workoutsApi, type PlannedCardioBlockDto } from '@/api/workouts';
import type { ExerciseDto } from '@/types/workout';
import { CardioSettingsDialog } from './CardioSettingsDialog';
import { stairStage1Block, stairStage2PendingBlock, walkBlock, walkPendingBlock } from './gymFixtures';

function renderDialog(block: PlannedCardioBlockDto, exerciseId: string, onClose = vi.fn(), onChosen?: (values: { intensity: number; speed?: number | null }) => void) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <CardioSettingsDialog open workoutId="w-v3" exerciseId={exerciseId} name="Cardio" block={block} onClose={onClose} onChosen={onChosen} />
    </QueryClientProvider>
  );
  return onClose;
}

function setOnline(online: boolean) {
  Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => online });
}

describe('CardioSettingsDialog', () => {
  const save = vi.fn(async () => ({ id: 'x' }) as ExerciseDto);

  beforeEach(() => {
    save.mockClear();
    setOnline(true);
    vi.spyOn(workoutsApi, 'setCardioSettings').mockImplementation(save);
  });

  it('changes the walk later: prefilled with the current speed and incline', async () => {
    const onClose = renderDialog(walkBlock, 'v3-walk');

    expect(screen.getByRole('dialog')).toHaveTextContent('Change settings');
    expect(screen.getByLabelText('Speed (km/h)')).toHaveValue(5.5);
    expect(screen.getByLabelText('Incline (%)')).toHaveValue(5);
    fireEvent.change(screen.getByLabelText('Incline (%)'), { target: { value: '6' } });
    fireEvent.click(screen.getByTestId('cardio-settings-save'));

    await waitFor(() => expect(save).toHaveBeenCalledWith('w-v3', 'v3-walk', { intensity: 6, speed: 5.5 }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('changes the stair level later: prefilled with the current level', async () => {
    renderDialog(stairStage1Block, 'v3-stairs');

    expect(screen.getByRole('dialog')).toHaveTextContent('Change settings');
    expect(screen.getByLabelText('Level')).toHaveValue(6);
    fireEvent.click(screen.getByTestId('cardio-settings-save'));

    await waitFor(() => expect(save).toHaveBeenCalledWith('w-v3', 'v3-stairs', { intensity: 6 }));
  });

  it('with no signal, the first walk setup is kept on the phone and nothing is sent', () => {
    setOnline(false);
    const onChosen = vi.fn();
    const onClose = renderDialog(walkPendingBlock, 'v3-walk', vi.fn(), onChosen);

    fireEvent.change(screen.getByLabelText('Speed (km/h)'), { target: { value: '5.5' } });
    fireEvent.click(screen.getByTestId('cardio-settings-save'));

    expect(onChosen).toHaveBeenCalledWith({ intensity: 5, speed: 5.5 });
    expect(onClose).toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
  });

  it('with no signal, a change to settings already set waits for a connection', async () => {
    setOnline(false);
    const onChosen = vi.fn();
    renderDialog(walkBlock, 'v3-walk', vi.fn(), onChosen);

    fireEvent.click(screen.getByTestId('cardio-settings-save'));

    expect(await screen.findByRole('alert')).toHaveTextContent('No signal');
    expect(onChosen).not.toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
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

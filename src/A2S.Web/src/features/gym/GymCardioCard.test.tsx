import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useState } from 'react';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { workoutsApi } from '@/api/workouts';
import type { ExerciseDto } from '@/types/workout';
import { GymCardioCard } from './GymCardioCard';
import { cardioGymSession, stairStage1Block, walkPendingBlock } from './gymFixtures';
import { chooseCardioEffort, setHardCardioSkipped, tickCardio, updateCardio } from './gymCardio';
import type { GymSessionState } from './gymSession';

const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

function Harness({ initial, index, onStart, onKeep }: { initial: GymSessionState; index: number; onStart?: () => void; onKeep?: () => void }) {
  const [state, setState] = useState(initial);
  return (
    <QueryClientProvider client={client}>
      <GymCardioCard
        item={state.cardio[index]}
        workoutId={state.workoutId}
        onChange={(change) => setState((s) => updateCardio(s, index, change))}
        onEffort={(effort) => setState((s) => chooseCardioEffort(s, index, effort))}
        onTick={() => setState((s) => tickCardio(s, index))}
        onStart={onStart}
        onKeep={onKeep}
      />
      <output data-testid="state">{JSON.stringify(state.cardio[index])}</output>
    </QueryClientProvider>
  );
}

const itemState = () => JSON.parse(screen.getByTestId('state').textContent ?? '{}');
const save = vi.fn(async () => ({ id: 'x' }) as ExerciseDto);

describe('GymCardioCard', () => {
  beforeEach(() => {
    save.mockClear();
    vi.spyOn(workoutsApi, 'setCardioSettings').mockImplementation(save);
  });

  it('asks for walk speed and incline before starting when settings are pending', async () => {
    render(<Harness initial={cardioGymSession(stairStage1Block, walkPendingBlock)} index={1} />);

    fireEvent.click(screen.getByTestId('cardio-tick-v3-walk'));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('Set up your walk');
    expect(within(dialog).getByLabelText('Incline (%)')).toHaveValue(5);
    expect(itemState().totalSeconds).toBeUndefined();
    fireEvent.change(within(dialog).getByLabelText('Speed (km/h)'), { target: { value: '5.5' } });
    fireEvent.click(within(dialog).getByTestId('cardio-settings-save'));

    await waitFor(() => expect(save).toHaveBeenCalledWith('w-v3', 'v3-walk', { intensity: 5, speed: 5.5 }));
  });

  it('ticks the walk, then asks how it felt', () => {
    render(<Harness initial={cardioGymSession()} index={1} />);

    expect(screen.queryByRole('button', { name: /Easy/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('cardio-tick-v3-walk'));
    fireEvent.click(screen.getByRole('button', { name: /Easy/ }));

    expect(itemState()).toMatchObject({ done: true, effort: 'Easy', totalSeconds: 600 });
    expect(screen.getByTestId('cardio-tick-v3-walk')).toHaveAttribute('aria-pressed', 'true');
  });

  it('starts the stair climber wizard and logs effort, stopped early, pain and floors', () => {
    const onStart = vi.fn();
    render(<Harness initial={cardioGymSession()} index={0} onStart={onStart} />);

    expect(screen.getByTestId('cardio-v3-stairs')).toHaveTextContent('3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)');
    fireEvent.click(screen.getByTestId('cardio-start-v3-stairs'));
    fireEvent.click(screen.getByRole('button', { name: /Brutal/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Stopped early' }));
    fireEvent.click(screen.getByRole('button', { name: 'Pain or niggle' }));
    fireEvent.change(screen.getByLabelText('Floors'), { target: { value: '33' } });

    expect(onStart).toHaveBeenCalledTimes(1);
    expect(itemState()).toMatchObject({ done: true, effort: 'Brutal', finished: false, pain: 'Niggle', floors: 33, totalSeconds: 660 });
    expect(screen.getByRole('button', { name: 'Stopped early' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('has no Start button when the stair climber has no wizard (a finisher owns it)', () => {
    render(<Harness initial={cardioGymSession()} index={0} />);

    expect(screen.queryByTestId('cardio-start-v3-stairs')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Solid/ })).toBeInTheDocument();
  });

  it('shows a skipped stair climber with Do it anyway', () => {
    const onKeep = vi.fn();
    render(<Harness initial={setHardCardioSkipped(cardioGymSession(), true)} index={0} onStart={vi.fn()} onKeep={onKeep} />);

    expect(screen.getByTestId('cardio-v3-stairs')).toHaveTextContent('Skipped for recovery today');
    expect(screen.queryByTestId('cardio-start-v3-stairs')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Do it anyway' }));
    expect(onKeep).toHaveBeenCalledTimes(1);
  });
});

import { describe, it, expect, vi } from 'vitest';
import { useState } from 'react';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { GymChecklist } from './GymChecklist';
import { cardioGymSession, freshGymSession, midGymSession } from './gymFixtures';
import { allDrillsDone, editSet, tickDrill, toggleSet, updateFinisher, type GymSessionState } from './gymSession';

const rest = { remaining: 0, total: 0, running: false, onAdjust: vi.fn(), onSkip: vi.fn() };

function Harness({ initial, onFinish, onRest }: { initial: GymSessionState; onFinish: (s: GymSessionState) => void; onRest?: () => void }) {
  const [state, setState] = useState(initial);
  return (
    <GymChecklist
      state={state}
      rest={rest}
      online
      finishing={false}
      onTickDrill={(i) => setState((s) => tickDrill(s, i))}
      onAllDrillsDone={() => setState(allDrillsDone)}
      onToggleSet={(l, s, reps) => {
        onRest?.();
        setState((st) => toggleSet(st, l, s, reps));
      }}
      onEditSet={(l, s, c) => setState((st) => editSet(st, l, s, c))}
      onFinisherChange={(c) => setState((s) => updateFinisher(s, c))}
      onFinish={() => onFinish(state)}
    />
  );
}

describe('GymChecklist', () => {
  it('ticks the warm-up with All done and confirms a set with one tap', () => {
    const onRest = vi.fn();
    render(<Harness initial={freshGymSession()} onFinish={vi.fn()} onRest={onRest} />);

    fireEvent.click(screen.getByTestId('warm-up-all-done'));
    fireEvent.click(screen.getByTestId('set-0-0'));

    expect(screen.getByTestId('drill-0')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('set-0-0')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('gym-progress')).toHaveTextContent('1 of 8 sets done');
    expect(onRest).toHaveBeenCalledTimes(1);
  });

  it('asks for reps on the AMRAP set and records them', () => {
    render(<Harness initial={midGymSession()} onFinish={vi.fn()} />);

    fireEvent.click(screen.getByTestId('set-0-4'));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Reps done'), { target: { value: '9' } });
    fireEvent.click(within(dialog).getByTestId('amrap-save'));

    expect(screen.getByTestId('set-0-4')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('set-0-4')).toHaveAccessibleName(/75 kg × 9/);
  });

  it('confirms before finishing early and lists what is left out', () => {
    const onFinish = vi.fn();
    render(<Harness initial={midGymSession()} onFinish={onFinish} />);

    fireEvent.click(screen.getByTestId('gym-finish-early'));
    const dialog = screen.getByRole('dialog');

    expect(within(dialog).getByText('Seal Row (Barbell)')).toBeInTheDocument();
    expect(onFinish).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByTestId('confirm-finish-early'));
    expect(onFinish).toHaveBeenCalledTimes(1);
  });

  it('cannot finish before a set is ticked', () => {
    render(<Harness initial={freshGymSession()} onFinish={vi.fn()} />);

    expect(screen.getByTestId('gym-finish-early')).toBeDisabled();
  });

  it('offers Finish once everything is logged, including the finisher', () => {
    let state = allDrillsDone(freshGymSession());
    state.lifts.forEach((lift, li) => lift.sets.forEach((_, si) => (state = toggleSet(state, li, si))));
    render(<Harness initial={state} onFinish={vi.fn()} />);

    expect(screen.queryByTestId('gym-finish')).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Minutes'), { target: { value: '10' } });
    fireEvent.click(screen.getByRole('button', { name: /Solid/ }));

    expect(screen.getByTestId('gym-finish')).toBeEnabled();
  });

  it('lists the cardio exercises after the last lift block', () => {
    render(<Harness initial={cardioGymSession()} onFinish={vi.fn()} />);

    const stairs = screen.getByTestId('cardio-v3-stairs');
    const walk = screen.getByTestId('cardio-v3-walk');
    const lastLift = screen.getByTestId('set-5-0');

    expect(lastLift.compareDocumentPosition(stairs) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(stairs.compareDocumentPosition(walk) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getByTestId('gym-progress')).toHaveTextContent('0 of 16 sets done');
  });
});

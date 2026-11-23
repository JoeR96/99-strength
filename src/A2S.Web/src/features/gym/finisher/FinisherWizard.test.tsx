import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { buildFinisherPlan } from './finisherPlan';
import { pause, skip, startEngine } from './finisherEngine';
import { FinisherWizard } from './FinisherWizard';
import { memoryStorage } from './testStorage';

vi.mock('@/features/routines/playerEffects', () => ({ beep: vi.fn(), primeAudio: vi.fn(), useWakeLock: vi.fn() }));

const T0 = Date.parse('2026-11-23T18:00:00.000Z');
const phases = buildFinisherPlan({
  warmUp: { minutes: 3, level: 3, zone: 'Z2' },
  coolDown: { minutes: 3, level: 3, zone: 'Z2' },
  workMinutes: 14,
  level: 6,
  intervals: { rounds: 7, workSeconds: 60, restSeconds: 60 },
});

function renderAt(seconds: number, initial = startEngine('gym-ui', phases, T0), props: Partial<Parameters<typeof FinisherWizard>[0]> = {}) {
  const onDone = vi.fn();
  const onAbandon = vi.fn();
  const onSchedule = vi.fn();
  render(
    <FinisherWizard name="Stair finisher" clientSessionId="gym-ui" phases={phases} clock={() => T0 + seconds * 1000} initial={initial} onDone={onDone} onAbandon={onAbandon} onSchedule={onSchedule} initialSpeak={false} {...props} />
  );
  return { onDone, onAbandon, onSchedule };
}

describe('FinisherWizard', () => {
  beforeEach(() => {
    memoryStorage();
  });

  it('shows the warm-up with level, zone, countdown and what is next', () => {
    renderAt(20);

    expect(screen.getByTestId('finisher-wizard-phase')).toHaveTextContent('Warm-up');
    expect(screen.getByText('Level 3 · Z2')).toBeInTheDocument();
    expect(screen.getByTestId('finisher-wizard-countdown')).toHaveTextContent('2:40');
    expect(screen.getByTestId('finisher-wizard-next')).toHaveTextContent('Next: 1 min hard @ L6');
  });

  it('shows the round counter in the intervals and every control is at least 44 px', () => {
    renderAt(180 + 120 * 3 + 10);

    expect(screen.getByTestId('finisher-wizard-phase')).toHaveTextContent('Hard');
    expect(screen.getByText(/Round 4 of 7/)).toBeInTheDocument();
    for (const name of ['Back', '30 seconds less', 'Pause', '30 seconds more', 'Skip phase']) {
      expect(screen.getByRole('button', { name }).className).toMatch(/h-16/);
    }
  });

  it('pausing posts an empty schedule; skipping posts the new one', () => {
    const { onSchedule } = renderAt(20);

    fireEvent.click(screen.getByRole('button', { name: 'Pause' }));
    expect(onSchedule).toHaveBeenLastCalledWith([]);
    expect(screen.getByText('Paused')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Resume' }));
    fireEvent.click(screen.getByRole('button', { name: 'Skip phase' }));
    expect(screen.getByTestId('finisher-wizard-phase')).toHaveTextContent('Hard');
    expect(onSchedule.mock.lastCall![0][0].body).toBe('Easy: level 3 for 1 min');
  });

  it('abandon asks first, then leaves without logging', () => {
    const { onAbandon, onDone } = renderAt(20);

    fireEvent.click(screen.getByRole('button', { name: 'Abandon finisher' }));
    fireEvent.click(screen.getByTestId('finisher-wizard-abandon'));

    expect(onAbandon).toHaveBeenCalled();
    expect(onDone).not.toHaveBeenCalled();
  });

  it('finishes with a summary that logs total, work and level', () => {
    const total = phases.reduce((a, p) => a + p.seconds, 0);
    const { onDone } = renderAt(total + 30);

    expect(screen.getByTestId('finisher-wizard-summary')).toHaveTextContent('Finisher done');
    fireEvent.click(screen.getByTestId('finisher-wizard-log'));
    expect(onDone).toHaveBeenCalledWith({ totalSeconds: total, workSeconds: 14 * 60, level: 6, rounds: 7 });
  });

  it('shows Paused for a paused session', () => {
    renderAt(400, pause(skip(startEngine('gym-ui', phases, T0), T0 + 1000), T0 + 2000));

    expect(screen.getByText('Paused')).toBeInTheDocument();
    expect(screen.getByTestId('finisher-wizard-countdown')).toHaveTextContent('0:59');
  });
});

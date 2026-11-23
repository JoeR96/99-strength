import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { GymChecklist } from './GymChecklist';
import { freshGymSession, trackFinisherGymSession } from './gymFixtures';

const rest = { remaining: 0, total: 0, running: false, onAdjust: vi.fn(), onSkip: vi.fn() };
const noop = () => {};

function renderChecklist(state = trackFinisherGymSession(), onStartFinisher?: () => void, finisherStartLabel?: string) {
  render(
    <GymChecklist
      state={state}
      rest={rest}
      online
      finishing={false}
      onTickDrill={noop}
      onAllDrillsDone={noop}
      onToggleSet={noop}
      onEditSet={noop}
      onFinisherChange={noop}
      onFinish={noop}
      onStartFinisher={onStartFinisher}
      finisherStartLabel={finisherStartLabel}
    />
  );
}

describe('Start finisher on the finisher card', () => {
  it('launches the live wizard from the stair finisher card', () => {
    const onStart = vi.fn();
    renderChecklist(trackFinisherGymSession(), onStart);

    fireEvent.click(screen.getByTestId('finisher-start'));

    expect(onStart).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId('finisher-start')).toHaveTextContent('Start finisher');
  });

  it('says Resume when a wizard is still running on this phone', () => {
    renderChecklist(trackFinisherGymSession(), vi.fn(), 'Resume finisher');

    expect(screen.getByTestId('finisher-start')).toHaveTextContent('Resume finisher');
  });

  it('has no button without a wizard (protocol finisher)', () => {
    renderChecklist(freshGymSession());

    expect(screen.queryByTestId('finisher-start')).not.toBeInTheDocument();
  });
});

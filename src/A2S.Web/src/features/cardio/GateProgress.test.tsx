import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { GateProgress } from './GateProgress';
import { runningGate } from '@/mocks/fixtures/gates';

describe('GateProgress', () => {
  it('lists each condition with its progress and no Start while any is unmet', () => {
    render(<GateProgress trackName="Walk → run" gate={runningGate(false)} onStart={vi.fn()} />);

    expect(screen.getByText(/Not started — gate · 0 of 3 met/)).toBeInTheDocument();
    expect(screen.getByText('Trend 91.3 kg, 1.3 kg to go')).toBeInTheDocument();
    expect(screen.getByText('1 of 2 weeks')).toBeInTheDocument();
    expect(screen.getAllByText('(not met yet)', { exact: false })).toHaveLength(3);
    expect(screen.queryByRole('button', { name: /Start/ })).not.toBeInTheDocument();
  });

  it('suggests Start when every condition is met and leaves it to the person', () => {
    const onStart = vi.fn();
    render(<GateProgress trackName="Walk → run" gate={runningGate(true)} onStart={onStart} />);

    const start = screen.getByRole('button', { name: 'Start Walk → run' });
    expect(start).toHaveClass('h-11');
    expect(onStart).not.toHaveBeenCalled();
    fireEvent.click(start);
    expect(onStart).toHaveBeenCalledTimes(1);
  });
});

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PhaseHeadsUpCard } from './PhaseHeadsUpCard';
import { buildHeadsUp } from '@/mocks/fixtures/phaseHeadsUp';

describe('PhaseHeadsUpCard', () => {
  it('shows the phase, when it starts and its changes', () => {
    render(<PhaseHeadsUpCard headsUp={buildHeadsUp(1)} onSwap={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Build starts tomorrow' })).toBeInTheDocument();
    expect(screen.getByText('Stair finisher: start intervals')).toBeInTheDocument();
  });

  it('says "starts today" on the first day', () => {
    render(<PhaseHeadsUpCard headsUp={buildHeadsUp(0)} onSwap={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Build starts today' })).toBeInTheDocument();
  });

  it('swaps only after the person confirms', () => {
    const onSwap = vi.fn();
    const headsUp = buildHeadsUp(3);
    render(<PhaseHeadsUpCard headsUp={headsUp} onSwap={onSwap} />);

    fireEvent.click(screen.getByRole('button', { name: 'Swap Pause Squat → Squat (Barbell)' }));
    expect(onSwap).not.toHaveBeenCalled();
    expect(screen.getByText(/Replace Pause Squat with Squat \(Barbell\) for good\?/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Swap' }));
    expect(onSwap).toHaveBeenCalledWith(headsUp.swaps[0]);
  });

  it('cancel leaves the workout alone', () => {
    const onSwap = vi.fn();
    render(<PhaseHeadsUpCard headsUp={buildHeadsUp(3)} onSwap={onSwap} />);

    fireEvent.click(screen.getByRole('button', { name: /Swap Pause Squat/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onSwap).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /Swap Pause Squat/ })).toBeInTheDocument();
  });

  it('a swap already made shows as done with no button', () => {
    render(<PhaseHeadsUpCard headsUp={buildHeadsUp(0, true)} onSwap={vi.fn()} />);

    expect(screen.getByText('Squat (Barbell) is already in your workout.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Swap/ })).not.toBeInTheDocument();
  });
});

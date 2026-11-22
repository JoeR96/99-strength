import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ReadinessCard } from './ReadinessCard';
import { readinessFixture } from '@/mocks/handlers/readiness';

const day = '2026-11-22';

describe('ReadinessCard', () => {
  it('offers Green / Yellow / Red as 44 px toggle buttons and reports the tap', () => {
    const onSelect = vi.fn();
    render(<ReadinessCard readiness={readinessFixture(day, null)} onSelect={onSelect} onKeepFinisher={vi.fn()} />);

    const red = screen.getByRole('button', { name: 'Red' });
    expect(red).toHaveClass('h-11');
    expect(red).toHaveAttribute('aria-pressed', 'false');
    fireEvent.click(red);
    expect(onSelect).toHaveBeenCalledWith('Red');
    expect(screen.queryByTestId('finisher-status')).not.toBeInTheDocument();
  });

  it('on red shows the finisher skipped for recovery with one tap to undo', () => {
    const onKeep = vi.fn();
    render(<ReadinessCard readiness={readinessFixture(day, 'Red')} onSelect={vi.fn()} onKeepFinisher={onKeep} />);

    expect(screen.getByRole('button', { name: 'Red' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('finisher-status')).toHaveTextContent('Finisher: Skipped for recovery');
    expect(screen.getByText('Walks and home routines are fine.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    expect(onKeep).toHaveBeenCalledWith(true);
  });

  it('on red with the finisher kept offers to skip it again', () => {
    const onKeep = vi.fn();
    render(<ReadinessCard readiness={readinessFixture(day, 'Red', true)} onSelect={vi.fn()} onKeepFinisher={onKeep} />);

    expect(screen.getByTestId('finisher-status')).toHaveTextContent('Finisher: kept');
    fireEvent.click(screen.getByRole('button', { name: 'Skip it' }));
    expect(onKeep).toHaveBeenCalledWith(false);
  });

  it('on yellow shows a gentle note and nothing about the finisher', () => {
    render(<ReadinessCard readiness={readinessFixture(day, 'Yellow')} onSelect={vi.fn()} onKeepFinisher={vi.fn()} />);

    expect(screen.getByText(/Nothing in your plan changes/)).toBeInTheDocument();
    expect(screen.queryByTestId('finisher-status')).not.toBeInTheDocument();
  });
});

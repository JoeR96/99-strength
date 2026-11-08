import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { WeeklyRings } from './WeeklyRings';
import { ringProgress, allowanceState, ringsFor } from './rings';
import { weeklyGoalsFixture } from '@/mocks/fixtures/plan';

describe('WeeklyRings maths', () => {
  it('ringProgress clamps between 0 and 1 and is 0 when nothing is planned', () => {
    expect(ringProgress(1, 4)).toBe(0.25);
    expect(ringProgress(4, 4)).toBe(1);
    expect(ringProgress(5, 4)).toBe(1);
    expect(ringProgress(0, 0)).toBe(0);
    expect(ringProgress(2, 0)).toBe(0);
  });

  it('allowanceState fills as you spend and flags going over', () => {
    expect(allowanceState(0, 1)).toEqual({ progress: 0, over: false });
    expect(allowanceState(1, 1)).toEqual({ progress: 1, over: false });
    expect(allowanceState(2, 1)).toEqual({ progress: 1, over: true });
    expect(allowanceState(1, 0)).toEqual({ progress: 1, over: true });
  });

  it('a paused week (nothing planned) gives muted session rings', () => {
    const rings = ringsFor(weeklyGoalsFixture('2026-11-09', { liftsPlanned: 0, cardioPlanned: 0, mobilityPlanned: 0 }));
    expect(rings.filter((r) => r.tone === 'muted').map((r) => r.key)).toEqual(['lifts', 'cardio', 'mobility', 'meal', 'alcohol']);
  });

  it('complete session goals turn green', () => {
    const rings = ringsFor(weeklyGoalsFixture('2026-11-09', { liftsDone: 4, liftsPlanned: 4 }));
    expect(rings.find((r) => r.key === 'lifts')?.tone).toBe('success');
  });
});

describe('WeeklyRings', () => {
  it('renders five rings with done/planned text', () => {
    render(<WeeklyRings week={weeklyGoalsFixture('2026-11-09', { liftsDone: 2 })} />);
    expect(screen.getByTestId('ring-lifts')).toHaveTextContent('2/4');
    expect(screen.getByTestId('ring-cardio')).toHaveTextContent('0/2');
    expect(screen.getByTestId('ring-mobility')).toHaveTextContent('0/1');
    expect(screen.getByTestId('ring-meal')).toHaveTextContent('0/1');
    expect(screen.getByTestId('ring-alcohol')).toHaveTextContent('0/1');
    expect(screen.getByLabelText('Lifts: 2 of 4')).toBeInTheDocument();
  });
});

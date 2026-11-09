import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { HabitCounter, LONG_PRESS_MS } from './HabitCounter';
import type { HabitTodayDto } from '@/types/habits';

const water: HabitTodayDto = {
  id: 'water',
  name: 'Water',
  type: 'Counter',
  target: 6,
  minimum: 4,
  unit: 'bottles',
  value: 2,
  metMinimum: false,
  metTarget: false,
  order: 1,
  icon: 'droplets',
};

afterEach(() => vi.useRealTimers());

describe('HabitCounter', () => {
  it('renders value over target and a 64 px plus button', () => {
    render(<HabitCounter habit={water} onTap={vi.fn()} />);
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('/ 6')).toBeInTheDocument();
    const plus = screen.getByRole('button', { name: /add one water/i });
    expect(plus.className).toMatch(/\bh-16\b/);
    expect(plus.className).toMatch(/\bw-16\b/);
  });

  it('a double-tap sends two +1 taps (no debouncing; the server reconciles)', () => {
    const onTap = vi.fn();
    render(<HabitCounter habit={water} onTap={onTap} />);
    const plus = screen.getByRole('button', { name: /add one water/i });
    fireEvent.click(plus);
    fireEvent.click(plus);
    expect(onTap).toHaveBeenCalledTimes(2);
    expect(onTap).toHaveBeenNthCalledWith(1, 'water', 1);
    expect(onTap).toHaveBeenNthCalledWith(2, 'water', 1);
  });

  it('a long press sends −1 and swallows the click that follows', () => {
    vi.useFakeTimers();
    const onTap = vi.fn();
    render(<HabitCounter habit={water} onTap={onTap} />);
    const plus = screen.getByRole('button', { name: /add one water/i });
    fireEvent.pointerDown(plus, { button: 0, pointerType: 'touch' });
    act(() => {
      vi.advanceTimersByTime(LONG_PRESS_MS);
    });
    fireEvent.pointerUp(plus);
    fireEvent.click(plus);
    expect(onTap).toHaveBeenCalledTimes(1);
    expect(onTap).toHaveBeenCalledWith('water', -1);
  });

  it('a short press is a normal +1', () => {
    vi.useFakeTimers();
    const onTap = vi.fn();
    render(<HabitCounter habit={water} onTap={onTap} />);
    const plus = screen.getByRole('button', { name: /add one water/i });
    fireEvent.pointerDown(plus, { button: 0, pointerType: 'touch' });
    act(() => {
      vi.advanceTimersByTime(LONG_PRESS_MS - 100);
    });
    fireEvent.pointerUp(plus);
    fireEvent.click(plus);
    expect(onTap).toHaveBeenCalledTimes(1);
    expect(onTap).toHaveBeenCalledWith('water', 1);
  });

  it('the small minus button sends −1 and is disabled at zero', () => {
    const onTap = vi.fn();
    const { rerender } = render(<HabitCounter habit={water} onTap={onTap} />);
    fireEvent.click(screen.getByRole('button', { name: /one less water/i }));
    expect(onTap).toHaveBeenCalledWith('water', -1);
    rerender(<HabitCounter habit={{ ...water, value: 0 }} onTap={onTap} />);
    expect(screen.getByRole('button', { name: /one less water/i })).toBeDisabled();
  });

  it('shows the minimum tick in amber and the target tick in green', () => {
    const { rerender } = render(<HabitCounter habit={{ ...water, value: 4, metMinimum: true }} onTap={vi.fn()} />);
    expect(screen.getByLabelText('Minimum met')).toBeInTheDocument();
    rerender(<HabitCounter habit={{ ...water, value: 6, metMinimum: true, metTarget: true }} onTap={vi.fn()} />);
    expect(screen.getByLabelText('Target met')).toBeInTheDocument();
  });
});

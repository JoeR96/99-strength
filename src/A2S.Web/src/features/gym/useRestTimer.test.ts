import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { formatClock, useRestTimer } from './useRestTimer';
import { restAfterSet, toggleSet } from './gymSession';
import { planV3GymSession } from './gymFixtures';

describe('useRestTimer', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-16T18:00:00.000Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('counts down from the rest seconds and beeps once at zero', () => {
    const onEnd = vi.fn();
    const { result } = renderHook(() => useRestTimer(onEnd));

    act(() => result.current.start(90));
    expect(result.current).toMatchObject({ running: true, remaining: 90, total: 90 });

    act(() => vi.advanceTimersByTime(30_000));
    expect(result.current.remaining).toBe(60);
    expect(onEnd).not.toHaveBeenCalled();

    act(() => vi.advanceTimersByTime(60_000));
    expect(result.current.running).toBe(false);
    expect(result.current.remaining).toBe(0);
    expect(onEnd).toHaveBeenCalledTimes(1);

    act(() => vi.advanceTimersByTime(60_000));
    expect(onEnd).toHaveBeenCalledTimes(1);
  });

  it('restarts when the next set is confirmed', () => {
    const { result } = renderHook(() => useRestTimer());
    act(() => result.current.start(180));
    act(() => vi.advanceTimersByTime(100_000));

    act(() => result.current.start(90));

    expect(result.current.remaining).toBe(90);
    expect(result.current.total).toBe(90);
  });

  it('adds or removes 15 seconds, and Skip stops without a beep', () => {
    const onEnd = vi.fn();
    const { result } = renderHook(() => useRestTimer(onEnd));
    act(() => result.current.start(60));

    act(() => result.current.adjust(15));
    expect(result.current.remaining).toBe(75);
    act(() => result.current.adjust(-30));
    expect(result.current.remaining).toBe(45);

    act(() => result.current.skip());
    act(() => vi.advanceTimersByTime(60_000));
    expect(result.current.running).toBe(false);
    expect(onEnd).not.toHaveBeenCalled();
  });
});

describe('useRestTimer in a superset', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('runs no timer after the A set and the 60 s superset rest after the B set', () => {
    const onEnd = vi.fn();
    const { result } = renderHook(() => useRestTimer(onEnd));
    let state = planV3GymSession();

    act(() => result.current.start(restAfterSet(state, 1, 0)));
    expect(result.current.running).toBe(false);
    state = toggleSet(state, 1, 0);

    act(() => result.current.start(restAfterSet(state, 2, 0)));
    expect(result.current).toMatchObject({ running: true, remaining: 60, total: 60 });
  });

  it('stops a running rest when the A set of the next round is confirmed', () => {
    const { result } = renderHook(() => useRestTimer());
    act(() => result.current.start(60));

    act(() => result.current.start(null));

    expect(result.current.running).toBe(false);
  });
});

describe('formatClock', () => {
  it('formats minutes and seconds', () => {
    expect(formatClock(95)).toBe('1:35');
    expect(formatClock(5)).toBe('0:05');
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { buildFinisherPlan } from './finisherPlan';
import { loadEngine } from './finisherEngine';
import { useFinisherEngine } from './useFinisherEngine';
import { memoryStorage } from './testStorage';

const phases = buildFinisherPlan({
  warmUp: { minutes: 3, level: 3, zone: 'Z2' },
  coolDown: { minutes: 3, level: 3, zone: 'Z2' },
  workMinutes: 4,
  level: 6,
  intervals: { rounds: 2, workSeconds: 60, restSeconds: 60 },
});

describe('useFinisherEngine', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-23T18:00:00.000Z'));
    memoryStorage();
  });
  afterEach(() => vi.useRealTimers());

  it('ticks down, posts the schedule on taps, and saves to localStorage', () => {
    const onSchedule = vi.fn();
    const { result } = renderHook(() => useFinisherEngine({ clientSessionId: 'gym-1', phases, onSchedule }));
    expect(result.current.view).toBeNull();

    act(() => result.current.start());
    expect(onSchedule).toHaveBeenCalledTimes(1);
    expect(onSchedule.mock.calls[0][0][0]).toEqual({ at: '2026-11-23T18:03:00.000Z', body: 'Hard: level 6 for 1 min' });

    act(() => vi.advanceTimersByTime(10_000));
    expect(result.current.view!.remainingMs).toBe(170_000);

    act(() => result.current.pause());
    expect(onSchedule).toHaveBeenLastCalledWith([]);
    expect(loadEngine('gym-1')!.pausedAt).not.toBeNull();
  });

  it('re-derives at once when the page comes back from the background', () => {
    const { result } = renderHook(() => useFinisherEngine({ clientSessionId: 'gym-2', phases, tickMs: 60_000 }));
    act(() => result.current.start());

    // The page was suspended (no timers ran) for 3 min 30 s, then the person switches back.
    vi.setSystemTime(new Date('2026-11-23T18:03:30.000Z'));
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'));
    });

    expect(result.current.view!.phase).toMatchObject({ kind: 'hard', round: 1 });
    expect(result.current.view!.remainingMs).toBe(30_000);
    expect(loadEngine('gym-2')!.index).toBe(1);
  });

  it('resumes after a reload from what was saved', () => {
    const first = renderHook(() => useFinisherEngine({ clientSessionId: 'gym-3', phases }));
    act(() => first.result.current.start());
    first.unmount();

    vi.setSystemTime(new Date('2026-11-23T18:04:10.000Z'));
    const { result } = renderHook(() => useFinisherEngine({ clientSessionId: 'gym-3', phases }));

    expect(result.current.view!.phase).toMatchObject({ kind: 'easy', round: 1 });
  });
});

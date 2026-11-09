import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  HEVY_WEB_FALLBACK_MS,
  consumeHevyReturn,
  hevyRoutineAppUrl,
  hevyRoutineWebUrl,
  openHevyRoutine,
} from './hevyDeepLink';

describe('hevyDeepLink', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    consumeHevyReturn();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('builds the app deep link and the web fallback for a routine id', () => {
    expect(hevyRoutineAppUrl('rt_upper_a_w1')).toBe('hevy://routine/rt_upper_a_w1');
    expect(hevyRoutineWebUrl('rt_upper_a_w1')).toBe('https://hevy.com/routine/rt_upper_a_w1');
    expect(hevyRoutineWebUrl('a b/c')).toBe('https://hevy.com/routine/a%20b%2Fc');
  });

  it('hands over to the app straight away and opens the web routine after 800 ms when still visible', () => {
    const navigate = vi.fn();
    const openWeb = vi.fn();

    openHevyRoutine('rt_1', { navigate, openWeb, isVisible: () => true });

    expect(navigate).toHaveBeenCalledWith('hevy://routine/rt_1');
    vi.advanceTimersByTime(HEVY_WEB_FALLBACK_MS - 1);
    expect(openWeb).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(openWeb).toHaveBeenCalledTimes(1);
    expect(openWeb).toHaveBeenCalledWith('https://hevy.com/routine/rt_1');
  });

  it('does not open the web routine when the app took over (the page is hidden at 800 ms)', () => {
    const openWeb = vi.fn();

    openHevyRoutine('rt_1', { navigate: vi.fn(), openWeb, isVisible: () => false });
    vi.advanceTimersByTime(HEVY_WEB_FALLBACK_MS + 50);

    expect(openWeb).not.toHaveBeenCalled();
  });

  it('a frozen timer that only fires after the person comes back does not open the web routine', () => {
    const openWeb = vi.fn();
    let visible = true;

    openHevyRoutine('rt_1', { navigate: vi.fn(), openWeb, isVisible: () => visible });
    // Hevy opens: the page is hidden before the timer has run, then shown again much later.
    visible = false;
    document.dispatchEvent(new Event('visibilitychange'));
    visible = true;
    document.dispatchEvent(new Event('visibilitychange'));
    vi.advanceTimersByTime(HEVY_WEB_FALLBACK_MS * 10);

    expect(openWeb).not.toHaveBeenCalled();
  });

  it('the returned cancel stops the fallback', () => {
    const openWeb = vi.fn();

    const cancel = openHevyRoutine('rt_1', { navigate: vi.fn(), openWeb, isVisible: () => true });
    cancel();
    vi.advanceTimersByTime(HEVY_WEB_FALLBACK_MS + 50);

    expect(openWeb).not.toHaveBeenCalled();
  });

  it('remembers leaving for Hevy exactly once', () => {
    expect(consumeHevyReturn()).toBe(false);

    openHevyRoutine('rt_1', { navigate: vi.fn(), openWeb: vi.fn(), isVisible: () => false });

    expect(consumeHevyReturn()).toBe(true);
    expect(consumeHevyReturn()).toBe(false);
  });

  it('by default opens the fallback in a new tab and cuts its opener', () => {
    const popup = { opener: {} as unknown };
    const open = vi.spyOn(window, 'open').mockReturnValue(popup as Window);
    try {
      openHevyRoutine('rt_1', { navigate: vi.fn(), isVisible: () => true });
      vi.advanceTimersByTime(HEVY_WEB_FALLBACK_MS);

      expect(open).toHaveBeenCalledTimes(1);
      expect(open).toHaveBeenCalledWith('https://hevy.com/routine/rt_1', '_blank');
      expect(popup.opener).toBeNull();
    } finally {
      open.mockRestore();
    }
  });
});

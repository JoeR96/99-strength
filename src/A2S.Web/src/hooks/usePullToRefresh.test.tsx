import { describe, it, expect, vi, afterEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { PULL_TO_REFRESH_THRESHOLD_PX, usePullToRefresh } from './usePullToRefresh';

type TouchType = 'touchstart' | 'touchmove' | 'touchend' | 'touchcancel';

function touch(type: TouchType, clientY?: number, target: EventTarget = document.body) {
  const event = new Event(type, { bubbles: true });
  Object.defineProperty(event, 'touches', { value: clientY === undefined ? [] : [{ clientY }] });
  act(() => {
    target.dispatchEvent(event);
  });
}

describe('usePullToRefresh', () => {
  afterEach(() => {
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 0 });
    document.body.innerHTML = '';
  });

  it('refreshes when the finger is dragged past the threshold at the top of the page and let go', () => {
    const onRefresh = vi.fn();
    const { result } = renderHook(() => usePullToRefresh(onRefresh));

    touch('touchstart', 100);
    touch('touchmove', 100 + PULL_TO_REFRESH_THRESHOLD_PX + 10);
    expect(result.current.ready).toBe(true);
    expect(result.current.distance).toBeGreaterThanOrEqual(PULL_TO_REFRESH_THRESHOLD_PX);
    touch('touchend');

    expect(onRefresh).toHaveBeenCalledTimes(1);
    expect(result.current.distance).toBe(0);
  });

  it('does nothing for a short pull', () => {
    const onRefresh = vi.fn();
    const { result } = renderHook(() => usePullToRefresh(onRefresh));

    touch('touchstart', 100);
    touch('touchmove', 100 + PULL_TO_REFRESH_THRESHOLD_PX - 20);
    expect(result.current.ready).toBe(false);
    touch('touchend');

    expect(onRefresh).not.toHaveBeenCalled();
  });

  it('ignores a drag that starts further down the page', () => {
    const onRefresh = vi.fn();
    renderHook(() => usePullToRefresh(onRefresh));
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 240 });

    touch('touchstart', 100);
    touch('touchmove', 300);
    touch('touchend');

    expect(onRefresh).not.toHaveBeenCalled();
  });

  it('ignores a drag inside an open sheet', () => {
    const onRefresh = vi.fn();
    renderHook(() => usePullToRefresh(onRefresh));
    const sheet = document.createElement('div');
    sheet.setAttribute('role', 'dialog');
    const inner = document.createElement('button');
    sheet.appendChild(inner);
    document.body.appendChild(sheet);

    touch('touchstart', 100, inner);
    touch('touchmove', 300, inner);
    touch('touchend', undefined, inner);

    expect(onRefresh).not.toHaveBeenCalled();
  });

  it('a cancelled touch never refreshes', () => {
    const onRefresh = vi.fn();
    renderHook(() => usePullToRefresh(onRefresh));

    touch('touchstart', 100);
    touch('touchmove', 300);
    touch('touchcancel');

    expect(onRefresh).not.toHaveBeenCalled();
  });

  it('is inert when disabled, and calls the latest callback when enabled', () => {
    const first = vi.fn();
    const second = vi.fn();
    const { rerender } = renderHook(({ cb, enabled }) => usePullToRefresh(cb, { enabled }), {
      initialProps: { cb: first, enabled: false },
    });

    touch('touchstart', 100);
    touch('touchmove', 300);
    touch('touchend');
    expect(first).not.toHaveBeenCalled();

    rerender({ cb: second, enabled: true });
    touch('touchstart', 100);
    touch('touchmove', 300);
    touch('touchend');
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });

  it('switches off the native overscroll reload only while mounted', () => {
    const { unmount } = renderHook(() => usePullToRefresh(vi.fn()));
    expect(document.documentElement.style.overscrollBehaviorY).toBe('contain');
    unmount();
    expect(document.documentElement.style.overscrollBehaviorY).toBe('');
  });
});

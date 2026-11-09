import { describe, it, expect, afterEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useOnlineStatus } from './useOnlineStatus';

function setOnline(online: boolean) {
  Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => online });
  act(() => {
    window.dispatchEvent(new Event(online ? 'online' : 'offline'));
  });
}

describe('useOnlineStatus', () => {
  afterEach(() => {
    delete (navigator as { onLine?: boolean }).onLine;
  });

  it('follows the browser online and offline events', () => {
    const { result } = renderHook(() => useOnlineStatus());
    expect(result.current).toBe(true);

    setOnline(false);
    expect(result.current).toBe(false);

    setOnline(true);
    expect(result.current).toBe(true);
  });
});

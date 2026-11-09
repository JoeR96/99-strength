/**
 * Pull-to-refresh for a page scrolled to the top: drag down past the threshold and let go. Only
 * touch, so a mouse user keeps the Refresh button. Returns how far the finger has pulled so the
 * page can show a hint ("Release to refresh"). While it is on, the browser's own overscroll
 * reload is switched off so one gesture does not do both.
 */

import { useEffect, useRef, useState } from 'react';

export const PULL_TO_REFRESH_THRESHOLD_PX = 72;

export interface PullToRefreshOptions {
  enabled?: boolean;
  thresholdPx?: number;
}

/** A drag that starts inside an open sheet or dialog belongs to that sheet, not the page. */
function startsInOverlay(target: EventTarget | null): boolean {
  return target instanceof Element && target.closest('[role="dialog"], [role="alertdialog"]') !== null;
}

export function usePullToRefresh(
  onRefresh: () => void,
  { enabled = true, thresholdPx = PULL_TO_REFRESH_THRESHOLD_PX }: PullToRefreshOptions = {}
) {
  const [distance, setDistance] = useState(0);
  const refreshRef = useRef(onRefresh);

  useEffect(() => {
    refreshRef.current = onRefresh;
  }, [onRefresh]);

  useEffect(() => {
    if (!enabled) return;

    let startY: number | null = null;
    let pulled = 0;
    const setPull = (value: number) => {
      pulled = value;
      setDistance(value);
    };

    const onStart = (event: TouchEvent) => {
      const atTop = window.scrollY <= 0;
      startY = atTop && event.touches.length === 1 && !startsInOverlay(event.target) ? event.touches[0].clientY : null;
    };
    const onMove = (event: TouchEvent) => {
      if (startY === null) return;
      const delta = event.touches[0].clientY - startY;
      if (delta <= 0 || window.scrollY > 0) {
        setPull(0);
        return;
      }
      // Stop a little past the threshold so the hint does not run away from the finger.
      setPull(Math.min(delta, thresholdPx * 1.5));
    };
    const onEnd = () => {
      if (startY !== null && pulled >= thresholdPx) refreshRef.current();
      startY = null;
      setPull(0);
    };
    const onCancel = () => {
      startY = null;
      setPull(0);
    };

    const root = document.documentElement;
    const previousOverscroll = root.style.overscrollBehaviorY;
    root.style.overscrollBehaviorY = 'contain';

    document.addEventListener('touchstart', onStart, { passive: true });
    document.addEventListener('touchmove', onMove, { passive: true });
    document.addEventListener('touchend', onEnd);
    document.addEventListener('touchcancel', onCancel);
    return () => {
      root.style.overscrollBehaviorY = previousOverscroll;
      document.removeEventListener('touchstart', onStart);
      document.removeEventListener('touchmove', onMove);
      document.removeEventListener('touchend', onEnd);
      document.removeEventListener('touchcancel', onCancel);
    };
  }, [enabled, thresholdPx]);

  return { distance, ready: distance >= thresholdPx };
}

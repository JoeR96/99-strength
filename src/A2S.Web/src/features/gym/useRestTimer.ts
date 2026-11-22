/**
 * The rest timer between sets. `start(seconds)` (called when a set is confirmed) counts down from the
 * plan's rest; at zero it calls `onEnd` once (the beep) and stops. It counts against the wall clock,
 * not ticks, so a phone that throttles timers in the background still ends on time.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

export interface RestTimer {
  /** Whole seconds left; 0 when idle. */
  remaining: number;
  /** The length of the current rest. */
  total: number;
  running: boolean;
  start: (seconds: number) => void;
  /** Adds (or with a negative value, removes) time from the current rest. */
  adjust: (seconds: number) => void;
  skip: () => void;
}

export function useRestTimer(onEnd?: () => void, tickMs = 250): RestTimer {
  const [endsAt, setEndsAt] = useState<number | null>(null);
  const [total, setTotal] = useState(0);
  const [now, setNow] = useState(() => Date.now());
  const onEndRef = useRef(onEnd);
  useEffect(() => {
    onEndRef.current = onEnd;
  });

  useEffect(() => {
    if (endsAt === null) return;
    const id = setInterval(() => {
      const current = Date.now();
      setNow(current);
      if (current >= endsAt) {
        setEndsAt(null);
        onEndRef.current?.();
      }
    }, tickMs);
    return () => clearInterval(id);
  }, [endsAt, tickMs]);

  const start = useCallback((seconds: number) => {
    const current = Date.now();
    setNow(current);
    setTotal(seconds);
    setEndsAt(seconds > 0 ? current + seconds * 1000 : null);
  }, []);

  const adjust = useCallback((seconds: number) => {
    setEndsAt((end) => (end === null ? end : Math.max(Date.now(), end + seconds * 1000)));
    setTotal((t) => Math.max(0, t + seconds));
  }, []);

  const skip = useCallback(() => setEndsAt(null), []);

  const remaining = endsAt === null ? 0 : Math.max(0, Math.ceil((endsAt - now) / 1000));
  return { remaining, total, running: endsAt !== null, start, adjust, skip };
}

/** 95 → "1:35". */
export function formatClock(seconds: number): string {
  const s = Math.max(0, Math.round(seconds));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

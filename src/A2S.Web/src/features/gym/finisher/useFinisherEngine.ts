/**
 * Runs the finisher engine in React: resumes the saved state for this gym session (or none until
 * Start), re-derives "now" four times a second and instantly on visibilitychange / focus / pageshow,
 * saves every change to localStorage, and reports the phase-change schedule after each tap so the
 * server can notify while iOS has the page suspended. `clock` is injectable for stories and tests.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { FinisherPhase } from './finisherPlan';
import {
  adjust as adjustEngine,
  advance,
  back as backEngine,
  clearEngine,
  loadEngine,
  pause as pauseEngine,
  resume as resumeEngine,
  saveEngine,
  scheduleOf,
  skip as skipEngine,
  startEngine,
  viewOf,
  type FinisherEngineState,
  type ScheduledNudge,
} from './finisherEngine';

export interface FinisherEngineOptions {
  clientSessionId: string;
  phases: FinisherPhase[];
  clock?: () => number;
  /** Stories/tests: start from this state instead of localStorage. */
  initial?: FinisherEngineState | null;
  /** Called with the new schedule after Start and every pause, resume, skip, back or ±30 s. */
  onSchedule?: (nudges: ScheduledNudge[]) => void;
  tickMs?: number;
}

export function useFinisherEngine({ clientSessionId, phases, clock = Date.now, initial, onSchedule, tickMs = 250 }: FinisherEngineOptions) {
  const clockRef = useRef(clock);
  const scheduleRef = useRef(onSchedule);
  useEffect(() => {
    clockRef.current = clock;
    scheduleRef.current = onSchedule;
  });
  const [state, setState] = useState<FinisherEngineState | null>(() => (initial !== undefined ? initial : loadEngine(clientSessionId)));
  const stateRef = useRef(state);
  const [now, setNow] = useState(() => clock());

  const commit = useCallback((next: FinisherEngineState | null) => {
    stateRef.current = next;
    setState(next);
    if (next) saveEngine(next);
  }, []);

  const running = state !== null && state.pausedAt === null && state.index < state.phases.length;

  // Re-reads the clock and rolls the state over any phase that ended (saved, so a reload resumes there).
  const refresh = useCallback(() => {
    const at = clockRef.current();
    setNow(at);
    const current = stateRef.current;
    if (!current) return;
    const rolled = advance(current, at);
    if (rolled !== current) commit(rolled);
  }, [commit]);

  useEffect(() => {
    if (!running) return;
    const id = window.setInterval(refresh, tickMs);
    return () => window.clearInterval(id);
  }, [running, tickMs, refresh]);

  // Back from YouTube (or a reload, or a suspended tab): re-derive at once, don't wait for the next tick.
  useEffect(() => {
    document.addEventListener('visibilitychange', refresh);
    window.addEventListener('focus', refresh);
    window.addEventListener('pageshow', refresh);
    return () => {
      document.removeEventListener('visibilitychange', refresh);
      window.removeEventListener('focus', refresh);
      window.removeEventListener('pageshow', refresh);
    };
  }, [refresh]);

  const act = useCallback(
    (change: (s: FinisherEngineState, at: number) => FinisherEngineState) => {
      const current = stateRef.current;
      if (!current) return;
      const at = clockRef.current();
      const next = change(current, at);
      setNow(at);
      commit(next);
      scheduleRef.current?.(scheduleOf(next, at));
    },
    [commit]
  );

  const start = useCallback(() => {
    const at = clockRef.current();
    const next = startEngine(clientSessionId, phases, at);
    setNow(at);
    commit(next);
    scheduleRef.current?.(scheduleOf(next, at));
  }, [clientSessionId, phases, commit]);

  const reset = useCallback(() => {
    clearEngine(clientSessionId);
    stateRef.current = null;
    setState(null);
  }, [clientSessionId]);

  const view = useMemo(() => (state ? viewOf(state, now) : null), [state, now]);

  return {
    state,
    now,
    view,
    start,
    pause: useCallback(() => act(pauseEngine), [act]),
    resume: useCallback(() => act(resumeEngine), [act]),
    skip: useCallback(() => act(skipEngine), [act]),
    back: useCallback(() => act(backEngine), [act]),
    adjust: useCallback((seconds: number) => act((s, at) => adjustEngine(s, seconds, at)), [act]),
    refresh,
    reset,
  };
}
